using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.RoomAvailability;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionAssessmentPlanningService(
	IRepository<AdmissionAssessmentSession, Guid> sessions,
	IRepository<AdmissionAssessmentAttempt, Guid> attempts,
	IRepository<AdmissionAssessmentBookingBatch, Guid> batches,
	IRepository<AdmissionApplicant, Guid> applicants,
	IRepository<AdmissionApplicationStageInstance, Guid> stages,
	IRepository<AdmissionApplicationWorkflowInstance, Guid> instances,
	IRepository<Room, Guid> rooms,
	IRepository<Building, Guid> buildings,
	IRepository<Floor, Guid> floors,
	IRepository<Employee, Guid> employees,
	AdmissionAssessmentAccessService access,
	AdmissionAssessmentSessionService sessionService,
	AdmissionAssessmentSchedulingService scheduling,
	AdmissionInquiryPeopleService people,
	IAdmissionAssessmentResourceLock resourceLock,
	IRoomAvailabilityAppService roomService,
	AdmissionAssessmentCommandService commands) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionAssessmentSessionResourcesDto> GetResourcesAsync()
	{
		if (!await AsyncExecuter.AnyAsync(await access.GetScopedOfferingsAsync())) throw AdmissionAssessmentSchedulingService.Invalid("An authorized admission scope is required.");
		var buildingRows = (await buildings.GetListAsync()).ToDictionary(item => item.Id);
		var allowedBuildings = buildingRows.Keys.ToArray();
		var floorRows = (await floors.GetListAsync(item => Enumerable.Contains(allowedBuildings, item.BuildingId))).ToDictionary(item => item.Id);
		return new()
		{
			Rooms = (await rooms.GetListAsync()).Where(item => buildingRows.ContainsKey(item.BuildingId) && floorRows.ContainsKey(item.FloorId))
				.OrderBy(item => item.RoomName).Select(item => new AdmissionAssessmentRoomDto { Id = item.Id, RoomName = item.RoomName,
					BuildingId = item.BuildingId, BuildingName = buildingRows[item.BuildingId].BuildingName, FloorId = item.FloorId,
					FloorName = floorRows[item.FloorId].FloorName, Capacity = AdmissionAssessmentSchedulingService.UsableCapacity(item) }).ToList(),
			Interviewers = await GetInterviewersAsync(null)
		};
	}
	public async Task<List<AdmissionAssessmentStaffDto>> GetInterviewersAsync(string? search)
	{
		if (search?.Length > 128 || !await AsyncExecuter.AnyAsync(await access.GetScopedOfferingsAsync())) throw AdmissionAssessmentSchedulingService.Invalid("An authorized admission scope and valid staff search are required.");
		var query = (await employees.GetQueryableAsync()).Where(item => item.UserId.HasValue).ApplyAttendanceDateEligibility(DateOnly.FromDateTime(DateTime.UtcNow));
		if (!string.IsNullOrWhiteSpace(search)) { var value = search.Trim().ToLowerInvariant(); query = query.Where(item => ((item.FirstName ?? "") + " " + (item.LastName ?? "")).ToLower().Contains(value) || item.EmployeeCode.ToLower().Contains(value)); }
		var result = new List<AdmissionAssessmentStaffDto>();
		for (var offset = 0; result.Count < 50; offset += 100)
		{
			var rows = await AsyncExecuter.ToListAsync(query.OrderBy(item => item.FirstName).ThenBy(item => item.Id).Skip(offset).Take(100));
			var activeUsers = (await people.GetCounsellorsAsync(null, candidateIds: rows.Select(item => item.UserId!.Value).ToArray())).Select(item => item.Id).ToHashSet();
			foreach (var row in rows.Where(item => activeUsers.Contains(item.UserId!.Value) && EmployeeLifecycleEligibility.IsAttendanceEligibleOnDate(item.EmployeeStatusType, item.StatusEffectiveDate, item.StatusLastChangedAt, DateOnly.FromDateTime(DateTime.UtcNow))))
			{ result.Add(new() { Id = row.Id, Name = row.EmployeeName }); if (result.Count == 50) break; }
			if (rows.Count < 100) break;
		}
		return result;
	}

	public async Task<PagedResultDto<AdmissionAssessmentSessionDto>> GetListAsync(AdmissionAssessmentSessionListInput input)
	{
		if (input.SkipCount < 0 || input.MaxResultCount is < 1 or > 200 || input.Search?.Length > 128) throw AdmissionAssessmentSchedulingService.Invalid("Choose a valid session page.");
		var offeringQuery = await access.GetScopedOfferingsAsync();
		var selectedStandards = AdmissionAssessmentFilterValues.Standards(input.StandardIds);
		var query = (await sessions.WithDetailsAsync(item => item.Scopes)).Where(item =>
			offeringQuery.Any(offering => offering.Id == item.AdmissionOfferingId) ||
			item.Scopes.Any(link => offeringQuery.Any(offering => offering.Id == link.AdmissionOfferingId)));
		if (input.AdmissionOfferingId.HasValue) query = query.Where(item => item.AdmissionOfferingId == input.AdmissionOfferingId || item.Scopes.Any(link => link.AdmissionOfferingId == input.AdmissionOfferingId));
		if (input.WorkflowStageDefinitionId.HasValue) query = query.Where(item => item.WorkflowStageDefinitionId == input.WorkflowStageDefinitionId || item.Scopes.Any(link => link.WorkflowStageDefinitionId == input.WorkflowStageDefinitionId));
		if (!input.IncludeCancelled) query = query.Where(item => !item.IsCancelled);
		if (!string.IsNullOrWhiteSpace(input.Search)) { var search = input.Search.Trim().ToLowerInvariant(); query = query.Where(item => item.Name.ToLower().Contains(search) || item.Location.ToLower().Contains(search)); }
		if (input.BoardId.HasValue || input.MediumId.HasValue || input.StandardId.HasValue || selectedStandards.Length > 0 || input.AcademicYearId.HasValue)
		{
			var filtered = offeringQuery.Where(item => (!input.BoardId.HasValue || item.BoardId == input.BoardId) && (!input.MediumId.HasValue || item.MediumId == input.MediumId) && (!input.StandardId.HasValue || item.StandardId == input.StandardId));
			if (selectedStandards.Length > 0) filtered = filtered.Where(item => Enumerable.Contains(selectedStandards, item.StandardId));
			if (input.AcademicYearId.HasValue)
			{
				var cycleQuery = await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionCycle, Guid>>().GetQueryableAsync();
				filtered = filtered.Where(item => cycleQuery.Any(cycle => cycle.Id == item.AdmissionCycleId && cycle.AcademicYearId == input.AcademicYearId));
			}
			query = query.Where(item => filtered.Any(offering => offering.Id == item.AdmissionOfferingId) || item.Scopes.Any(link => filtered.Any(offering => offering.Id == link.AdmissionOfferingId)));
		}
		var total = await AsyncExecuter.LongCountAsync(query);
		var sort = (input.Sorting ?? "startsAt asc").ToLowerInvariant();
		var ordered = sort switch { "name asc" => query.OrderBy(item => item.Name).ThenBy(item => item.Id), "name desc" => query.OrderByDescending(item => item.Name).ThenBy(item => item.Id),
			"startsat desc" => query.OrderByDescending(item => item.StartsAt).ThenBy(item => item.Id), _ => query.OrderBy(item => item.StartsAt).ThenBy(item => item.Id) };
		var rows = await AsyncExecuter.ToListAsync(ordered.Skip(input.SkipCount).Take(input.MaxResultCount));
		await scheduling.EnrichReservationsAsync(rows);
		var scopes = await sessionService.GetScopesAsync();
		var staffIds = rows.Where(item => item.InterviewerId.HasValue).Select(item => item.InterviewerId!.Value).Distinct().ToArray();
		var names = (await employees.GetListAsync(item => Enumerable.Contains(staffIds, item.Id))).ToDictionary(item => item.Id, item => item.EmployeeName);
		return new(total, rows.Select(row => Map(row, scopes, names)).ToList());
	}

	public async Task<AdmissionAssessmentSessionDto> GetAsync(Guid id)
	{
		var session = await sessionService.GetAsync(id, false);
		await scheduling.EnrichReservationsAsync([session]);
		var names = session.InterviewerId.HasValue ? (await employees.GetListAsync(item => item.Id == session.InterviewerId)).ToDictionary(item => item.Id, item => item.EmployeeName) : [];
		return Map(session, await sessionService.GetScopesAsync(), names);
	}
	public async Task<List<AdmissionAssessmentInterviewSlotDto>> GetSlotsAsync(Guid id)
	{
		var session = await sessionService.GetAsync(id, false);
		if (session.IsCancelled || session.EndsAt <= DateTime.UtcNow) return [];
		var reserved = await attempts.GetListAsync(item => item.SessionId == id && item.Status != "cancelled" && item.Outcome != "waived" && item.SlotStartsAt.HasValue);
		return session.GetInterviewSlots().Where(slot => slot.StartsAt > DateTime.UtcNow && reserved.All(item => item.SlotStartsAt != slot.StartsAt))
			.Select(slot => new AdmissionAssessmentInterviewSlotDto { StartsAt = AdmissionAssessmentSessionService.ToUtcOffset(slot.StartsAt), EndsAt = AdmissionAssessmentSessionService.ToUtcOffset(slot.EndsAt) }).ToList();
	}

	public async Task<AdmissionAssessmentSessionPreviewDto> PreviewAsync(AdmissionAssessmentSessionPreviewInput input)
	{
		if (input == null || input.Session == null) throw AdmissionAssessmentSchedulingService.Invalid("Choose a valid session setup.");
		var plan = await PrepareAsync(input.Session, input.SessionId, input.SessionConcurrencyStamp);
		if (input.SkipCount < 0 || input.MaxResultCount is < 1 or > 200 || input.Search?.Length > 128) throw AdmissionAssessmentSchedulingService.Invalid("Choose a valid preview page.");
		var result = plan.Preview;
		var filtered = plan.Candidates.AsEnumerable();
		if (!string.IsNullOrWhiteSpace(input.Search)) filtered = filtered.Where(item => item.ApplicantName.Contains(input.Search.Trim(), StringComparison.OrdinalIgnoreCase) || item.ApplicationNumber.Contains(input.Search.Trim(), StringComparison.OrdinalIgnoreCase));
		var rows = filtered.ToList(); result.TotalCount = rows.Count; result.Items = rows.Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
		return result;
	}

	[UnitOfWork(true)]
	public async Task<AdmissionAssessmentSessionConfirmationDto> ConfirmAsync(AdmissionAssessmentSessionConfirmInput input)
	{
		if (input == null || input.Session == null || input.Bookings == null || input.Bookings.Any(item => item == null) || input.IdempotencyKey == Guid.Empty || input.Bookings.Count > 200 || input.Bookings.Select(item => item.ApplicationId).Distinct().Count() != input.Bookings.Count || input.Bookings.Any(item => string.IsNullOrWhiteSpace(item.ConcurrencyStamp)))
			throw AdmissionAssessmentSchedulingService.Invalid("Confirm up to 200 distinct applicants with their current versions.");
		await resourceLock.AcquireAsync(CurrentTenant.Id, input.Bookings.Select(item => item.ApplicationId).Append(input.IdempotencyKey).Append(input.Session.RoomId.GetValueOrDefault()).Append(input.Session.InterviewerId.GetValueOrDefault()));
		var requestFingerprint = Hash(JsonSerializer.Serialize(input));
		var existing = await batches.FindAsync(input.IdempotencyKey);
		if (existing != null)
		{
			if (existing.ActorId != CurrentUser.Id || existing.Fingerprint != requestFingerprint) throw AdmissionAssessmentSchedulingService.Invalid("This confirmation key was already used for a different request.");
			return new() { Session = await GetAsync(existing.SessionId), BookedCount = existing.BookedCount };
		}
		var plan = await PrepareAsync(input.Session, input.SessionId, input.SessionConcurrencyStamp);
		if (plan.Preview.PreviewFingerprint != input.PreviewFingerprint) throw new BusinessException(AdmissionAssessmentErrorCodes.ConcurrencyConflict, "Availability or eligible applicants changed. Refresh the booking preview.");
		if (input.Bookings.Count > plan.Preview.AvailableCount) throw new BusinessException(AdmissionAssessmentErrorCodes.CapacityExceeded, "There are not enough available seats or interview slots.");
		var candidates = plan.Candidates.ToDictionary(item => item.StageInstanceId);
		foreach (var booking in input.Bookings)
			if (!candidates.TryGetValue(booking.StageInstanceId, out var candidate) || candidate.ApplicationId != booking.ApplicationId || candidate.ConcurrencyStamp != booking.ConcurrencyStamp)
				throw new BusinessException(AdmissionAssessmentErrorCodes.ConcurrencyConflict, "A selected applicant is no longer eligible. Refresh the booking preview.");
		var session = plan.Existing ?? await CreateEntityAsync(input.Session, plan);
		var availableSlots = plan.Preview.Slots.Select(item => item.StartsAt).ToList();
		var explicitSlots = input.Bookings.Where(item => item.SlotStartsAt.HasValue).Select(item => item.SlotStartsAt!.Value).ToList();
		if (session.SessionType == "individualInterview" && (explicitSlots.Distinct().Count() != explicitSlots.Count || explicitSlots.Any(slot => !availableSlots.Contains(slot))))
			throw AdmissionAssessmentSchedulingService.Invalid("Select distinct available interview slots.");
		foreach (var slot in explicitSlots) availableSlots.Remove(slot);
		foreach (var booking in input.Bookings)
		{
			var slot = booking.SlotStartsAt;
			if (session.SessionType == "individualInterview")
			{
				var candidate = candidates[booking.StageInstanceId];
				slot ??= availableSlots.Where(value => !plan.Appointments.GetValueOrDefault(candidate.ApplicantId, []).Any(appointment => appointment.Start < value.UtcDateTime.AddMinutes(session.SlotDurationMinutes!.Value) && appointment.End > value.UtcDateTime)).Select(value => (DateTimeOffset?)value).FirstOrDefault();
				if (!slot.HasValue || !booking.SlotStartsAt.HasValue && !availableSlots.Remove(slot.Value)) throw AdmissionAssessmentSchedulingService.Invalid("A selected interview slot is no longer free or was selected twice.");
			}
			await commands.BookAsync(booking.ApplicationId, booking.StageInstanceId, new() { SessionId = session.Id, ConcurrencyStamp = booking.ConcurrencyStamp, SlotStartsAt = slot });
		}
		await batches.InsertAsync(new AdmissionAssessmentBookingBatch(input.IdempotencyKey, CurrentTenant.Id, session.Id,
			CurrentUser.Id ?? throw AdmissionAssessmentSchedulingService.Invalid("An authenticated staff account is required."), requestFingerprint, input.Bookings.Count), true);
		return new() { Session = await GetAsync(session.Id), BookedCount = input.Bookings.Count };
	}

	[UnitOfWork(true)]
	public async Task<AdmissionAssessmentSessionDto> CreateAsync(AdmissionAssessmentSessionInput input)
	{
		await resourceLock.AcquireAsync(CurrentTenant.Id, [input.RoomId.GetValueOrDefault(), input.InterviewerId.GetValueOrDefault()]);
		var plan = await PrepareAsync(input, null, null);
		var session = await CreateEntityAsync(input, plan);
		return await GetAsync(session.Id);
	}

	[UnitOfWork(true)]
	public async Task<AdmissionAssessmentSessionDto> UpdateAsync(Guid id, AdmissionAssessmentSessionInput input)
	{
		await resourceLock.AcquireAsync(CurrentTenant.Id, [id, input.RoomId.GetValueOrDefault(), input.InterviewerId.GetValueOrDefault()]);
		var current = await sessionService.GetAsync(id);
		AdmissionAssessmentAccessService.EnsureConcurrency(current, input.ConcurrencyStamp);
		if (current.StartsAt <= DateTime.UtcNow) throw AdmissionAssessmentSchedulingService.Invalid("A started session cannot be rescheduled.");
		var plan = await PrepareAsync(input, id, input.ConcurrencyStamp, true);
		if (current.BookedCount > 0 && current.SessionType == "individualInterview" &&
			(current.StartsAt != input.StartsAt.UtcDateTime || current.EndsAt != input.EndsAt.UtcDateTime || current.Capacity != input.Capacity || current.SlotDurationMinutes != input.SlotDurationMinutes || current.BreakMinutes != input.BreakMinutes || current.RoomId != input.RoomId || current.InterviewerId != input.InterviewerId))
			throw AdmissionAssessmentSchedulingService.Invalid("Cancel the unstarted interview bookings before changing their slot layout.");
		if (current.BookedCount > 0 && current.SessionType != "individualInterview")
		{
			var appQuery = await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionApplication, Guid>>().GetQueryableAsync();
			var attemptQuery = await attempts.GetQueryableAsync();
			var bookedApplicantIds = await AsyncExecuter.ToListAsync((from attempt in attemptQuery join app in appQuery on attempt.ApplicationId equals app.Id
				where attempt.SessionId == id && attempt.Status != "cancelled" && attempt.Outcome != "waived" select app.ApplicantId).Distinct());
			if ((await scheduling.GetApplicantAppointmentsAsync(bookedApplicantIds.ToArray(), input.StartsAt.UtcDateTime, input.EndsAt.UtcDateTime, id)).Count > 0)
				throw AdmissionAssessmentSchedulingService.Invalid("The proposed time conflicts with an applicant's other assessment appointment.");
		}
		var reservationId = current.RoomReservationId;
		var resourceChanged = current.RoomId != input.RoomId || current.StartsAt != input.StartsAt.UtcDateTime || current.EndsAt != input.EndsAt.UtcDateTime || current.InterviewerId != input.InterviewerId;
		if (!reservationId.HasValue || resourceChanged)
		{
			if (reservationId.HasValue) await LazyServiceProvider.LazyGetRequiredService<RoomAvailabilityAppService>().CancelAssessmentReservationAsync(reservationId.Value, id, "Assessment session rescheduled.");
			reservationId = (await roomService.CreateReservationAsync(Reservation(input, plan.Offerings))).Id;
		}
		else if (current.Name != input.Name.Trim())
			await LazyServiceProvider.LazyGetRequiredService<RoomAvailabilityAppService>().RenameAssessmentReservationAsync(reservationId.Value, id, input.Name.Trim());
		current.Update(input.Name, input.StartsAt.UtcDateTime, input.EndsAt.UtcDateTime, Location(plan.Room), input.Capacity);
		current.ConfigurePlanning(input.SessionType, input.RoomId!.Value, reservationId.Value, input.InterviewerId, input.SlotDurationMinutes, input.BreakMinutes,
			current.Scopes.Count > 0 ? current.Scopes : plan.Links.Select(link => new AdmissionAssessmentSessionScope(GuidGenerator.Create(), CurrentTenant.Id, current.Id, link.AdmissionOfferingId, link.WorkflowStageDefinitionId)));
		await sessions.UpdateAsync(current, true);
		return await GetAsync(id);
	}

	private async Task<Plan> PrepareAsync(AdmissionAssessmentSessionInput input, Guid? id, string? stamp, bool allowChanges = false)
	{
		if (input == null || input.Scopes == null || input.Scopes.Any(item => item == null)) throw AdmissionAssessmentSchedulingService.Invalid("Choose valid session scope links.");
		if ((input.StartsAt.UtcDateTime <= DateTime.UtcNow && !(id.HasValue && !allowChanges && input.SessionType == "individualInterview" && input.EndsAt.UtcDateTime > DateTime.UtcNow)) || input.EndsAt <= input.StartsAt || string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 128)
			throw AdmissionAssessmentSchedulingService.Invalid("Choose a name and future assessment time.");
		var links = input.Scopes.Count > 0 ? input.Scopes : [new AdmissionAssessmentSessionScopeInput { AdmissionOfferingId = input.AdmissionOfferingId, WorkflowStageDefinitionId = input.WorkflowStageDefinitionId }];
		if (links.Count is < 1 or > 20 || links.Any(item => item.AdmissionOfferingId == Guid.Empty || item.WorkflowStageDefinitionId == Guid.Empty) || links.Select(item => (item.AdmissionOfferingId, item.WorkflowStageDefinitionId)).Distinct().Count() != links.Count)
			throw AdmissionAssessmentSchedulingService.Invalid("Choose up to 20 distinct offering and assessment-stage links.");
		var offerings = new List<AdmissionOffering>(); var definitionRows = new Dictionary<Guid, AdmissionWorkflowStageDefinition>();
		foreach (var link in links) { var definition = await access.GetSessionDefinitionAsync(link.AdmissionOfferingId, link.WorkflowStageDefinitionId); definitionRows[definition.Id] = definition; if (offerings.All(item => item.Id != link.AdmissionOfferingId)) offerings.Add(await access.GetOfferingAsync(link.AdmissionOfferingId)); }
		var existing = id.HasValue ? await sessionService.GetAsync(id.Value) : null;
		if (existing != null)
		{
			AdmissionAssessmentAccessService.EnsureConcurrency(existing, stamp);
			if (existing.IsCancelled || existing.SessionType != input.SessionType || (existing.Scopes.Count > 0 ? existing.Scopes.Count : 1) != links.Count || links.Any(item => !existing.Supports(item.AdmissionOfferingId, item.WorkflowStageDefinitionId)))
				throw AdmissionAssessmentSchedulingService.Invalid("A session's assessment type and offering-stage links cannot change.");
			if (!allowChanges && (existing.Name != input.Name.Trim() || existing.StartsAt != input.StartsAt.UtcDateTime || existing.EndsAt != input.EndsAt.UtcDateTime || existing.Capacity != input.Capacity || existing.RoomId != input.RoomId || existing.InterviewerId != input.InterviewerId || existing.SlotDurationMinutes != input.SlotDurationMinutes || existing.BreakMinutes != input.BreakMinutes))
				throw AdmissionAssessmentSchedulingService.Invalid("Save session changes before previewing additional bookings.");
		}
		var room = await scheduling.ValidateAsync(input, offerings, id, existing?.RoomReservationId);
		var temporary = new AdmissionAssessmentSession(Guid.NewGuid(), CurrentTenant.Id, links[0].AdmissionOfferingId, links[0].WorkflowStageDefinitionId, input.Name, input.StartsAt.UtcDateTime, input.EndsAt.UtcDateTime, room.RoomName, input.Capacity);
		temporary.ConfigurePlanning(input.SessionType, room.Id, Guid.NewGuid(), input.InterviewerId, input.SlotDurationMinutes, input.BreakMinutes,
			links.Select(item => new AdmissionAssessmentSessionScope(Guid.NewGuid(), CurrentTenant.Id, temporary.Id, item.AdmissionOfferingId, item.WorkflowStageDefinitionId)));
		if (input.SessionType == "individualInterview" && temporary.GetInterviewSlots().Count < input.Capacity) throw AdmissionAssessmentSchedulingService.Invalid("Capacity exceeds the interview slots available in this time window.");
		var candidates = await CandidatesAsync(links, definitionRows);
		var appointments = await scheduling.GetApplicantAppointmentsAsync(candidates.Select(item => item.ApplicantId).Distinct().ToArray(), input.StartsAt.UtcDateTime, input.EndsAt.UtcDateTime);
		var occupiedSlots = existing == null ? [] : await attempts.GetListAsync(item => item.SessionId == existing.Id && item.Status != "cancelled" && item.Outcome != "waived" && item.SlotStartsAt.HasValue);
		var freeSlots = temporary.GetInterviewSlots().Where(slot => slot.StartsAt > DateTime.UtcNow && occupiedSlots.All(item => item.SlotStartsAt != slot.StartsAt)).ToList();
		var available = input.SessionType == "individualInterview" ? freeSlots.Count : Math.Max(0, input.Capacity - (existing?.BookedCount ?? 0));
		var proposed = new List<AdmissionAssessmentBookingCandidateDto>();
		var proposedSlots = freeSlots.ToList();
		candidates = candidates.GroupBy(item => item.ApplicantId).Select(group => group.First()).Where(candidate => input.SessionType == "individualInterview"
			? freeSlots.Any(slot => !appointments.GetValueOrDefault(candidate.ApplicantId, []).Any(appointment => appointment.Start < slot.EndsAt && appointment.End > slot.StartsAt))
			: !appointments.GetValueOrDefault(candidate.ApplicantId, []).Any()).ToList();
		foreach (var candidate in candidates)
		{
			if (proposed.Count >= Math.Min(available, 200)) break;
			if (input.SessionType == "individualInterview")
			{
				var index = proposedSlots.FindIndex(slot => !appointments.GetValueOrDefault(candidate.ApplicantId, []).Any(appointment => appointment.Start < slot.EndsAt && appointment.End > slot.StartsAt));
				if (index < 0) continue;
				candidate.SlotStartsAt = AdmissionAssessmentSessionService.ToUtcOffset(proposedSlots[index].StartsAt); candidate.SlotEndsAt = AdmissionAssessmentSessionService.ToUtcOffset(proposedSlots[index].EndsAt); proposedSlots.RemoveAt(index);
			}
			proposed.Add(candidate);
		}
		var building = await buildings.GetAsync(room.BuildingId); var floor = await floors.GetAsync(room.FloorId);
		if (floor.BuildingId != building.Id) throw AdmissionAssessmentSchedulingService.Invalid("The room floor must belong to its Institute Setup building.");
		// Selected application versions and appointment eligibility are validated separately during confirmation.
		// Unselected arrivals or metadata edits must not erase an otherwise valid staff selection.
		var fingerprint = Hash(JsonSerializer.Serialize(new { input, id, stamp, room.Capacity, room.DisabledSeatNumbersCsv, room.ReservedSeatNumbersCsv, available, Slots = freeSlots.Select(slot => new { slot.StartsAt, slot.EndsAt }) }));
		return new Plan(existing, room, offerings, links, candidates, appointments, new AdmissionAssessmentSessionPreviewDto { EligibleCount = candidates.Count, AvailableCount = available,
			RemainingCount = Math.Max(0, candidates.Count - available), RoomName = room.RoomName, BuildingName = building.BuildingName, FloorName = floor.FloorName,
			PreviewFingerprint = fingerprint, RequiresRoomApproval = existing != null ? !await scheduling.IsReservationApprovedAsync(existing) : !CurrentUser.Roles.Any(role => role.Equals("admin", StringComparison.OrdinalIgnoreCase) || role.Equals("administrator", StringComparison.OrdinalIgnoreCase)),
			ProposedBookings = proposed, Slots = freeSlots.Select(slot => new AdmissionAssessmentInterviewSlotDto { StartsAt = AdmissionAssessmentSessionService.ToUtcOffset(slot.StartsAt), EndsAt = AdmissionAssessmentSessionService.ToUtcOffset(slot.EndsAt) }).ToList() });
	}

	private async Task<List<AdmissionAssessmentBookingCandidateDto>> CandidatesAsync(List<AdmissionAssessmentSessionScopeInput> links, Dictionary<Guid, AdmissionWorkflowStageDefinition> definitionRows)
	{
		var offeringIds = links.Select(item => item.AdmissionOfferingId).Distinct().ToArray(); var stageIds = links.Select(item => item.WorkflowStageDefinitionId).Distinct().ToArray();
		var appQuery = await LazyServiceProvider.LazyGetRequiredService<AdmissionApplicationScopeAccessService>().GetScopedApplicationsAsync();
		var attemptQuery = await attempts.GetQueryableAsync();
		var query = from app in appQuery join stage in await stages.GetQueryableAsync() on app.CurrentStageInstanceId equals stage.Id
			join workflow in await instances.GetQueryableAsync() on stage.WorkflowInstanceId equals workflow.Id
			join applicant in await applicants.GetQueryableAsync() on app.ApplicantId equals applicant.Id
			where Enumerable.Contains(offeringIds, app.AdmissionOfferingId) && Enumerable.Contains(stageIds, stage.DefinitionStageId) && workflow.ApplicationId == app.Id &&
				workflow.Status == AdmissionWorkflowInstanceStatus.Active && workflow.WorkflowDefinitionId == app.WorkflowDefinitionId && stage.Status == AdmissionRuntimeStageStatus.Active &&
				(app.Status == AdmissionApplicationStatus.Draft || app.Status == AdmissionApplicationStatus.Submitted || app.Status == AdmissionApplicationStatus.InReview || app.Status == AdmissionApplicationStatus.Approved || app.Status == AdmissionApplicationStatus.Waitlisted || app.Status == AdmissionApplicationStatus.Offered || app.Status == AdmissionApplicationStatus.OfferAccepted)
			select new { App = app, Stage = stage, Name = applicant.FirstName + " " + (applicant.LastName ?? ""), Count = attemptQuery.Count(item => item.StageInstanceId == stage.Id && item.Status != "cancelled"),
				LatestOutcome = attemptQuery.Where(item => item.StageInstanceId == stage.Id && item.Status != "cancelled").OrderByDescending(item => item.AttemptNumber).Select(item => item.Outcome).FirstOrDefault() };
		var rows = await AsyncExecuter.ToListAsync(query.OrderBy(item => item.Stage.EnteredAt).ThenBy(item => item.App.ApplicationNumber).ThenBy(item => item.Stage.Id));
		return rows.Where(row => links.Any(link => link.AdmissionOfferingId == row.App.AdmissionOfferingId && link.WorkflowStageDefinitionId == row.Stage.DefinitionStageId) &&
			definitionRows[row.Stage.DefinitionStageId].WorkflowDefinitionId == row.App.WorkflowDefinitionId && row.Count < AdmissionAssessmentAccessService.ReadPolicy(definitionRows[row.Stage.DefinitionStageId])!.MaxAttempts && (row.LatestOutcome == null || row.LatestOutcome == "retake"))
			.Select(row => new AdmissionAssessmentBookingCandidateDto { ApplicationId = row.App.Id, StageInstanceId = row.Stage.Id, ApplicationNumber = row.App.ApplicationNumber,
				ApplicantId = row.App.ApplicantId, ApplicantName = row.Name.Trim(), AdmissionOfferingId = row.App.AdmissionOfferingId, WorkflowStageDefinitionId = row.Stage.DefinitionStageId, ConcurrencyStamp = row.App.ConcurrencyStamp }).ToList();
	}

	private async Task<AdmissionAssessmentSession> CreateEntityAsync(AdmissionAssessmentSessionInput input, Plan plan)
	{
		var reservation = await roomService.CreateReservationAsync(Reservation(input, plan.Offerings));
		var session = new AdmissionAssessmentSession(GuidGenerator.Create(), CurrentTenant.Id, plan.Links[0].AdmissionOfferingId, plan.Links[0].WorkflowStageDefinitionId,
			input.Name, input.StartsAt.UtcDateTime, input.EndsAt.UtcDateTime, Location(plan.Room), input.Capacity);
		session.ConfigurePlanning(input.SessionType, plan.Room.Id, reservation.Id, input.InterviewerId, input.SlotDurationMinutes, input.BreakMinutes,
			plan.Links.Select(link => new AdmissionAssessmentSessionScope(GuidGenerator.Create(), CurrentTenant.Id, session.Id, link.AdmissionOfferingId, link.WorkflowStageDefinitionId)));
		await sessions.InsertAsync(session, true); return session;
	}
	private static CreateRoomReservationDto Reservation(AdmissionAssessmentSessionInput input, IReadOnlyCollection<AdmissionOffering> offerings)
	{
		var window = AdmissionAssessmentSchedulingService.SchoolWindow(input.StartsAt.UtcDateTime, input.EndsAt.UtcDateTime);
		return new() { RoomId = input.RoomId!.Value, Date = window.Date, StartTime = window.Start, EndTime = window.End, Purpose = input.Name,
			ReservationType = input.SessionType == "exam" ? RoomReservationType.Exam : RoomReservationType.Meeting, TeacherId = input.InterviewerId,
			StandardId = offerings.Select(item => item.StandardId).Distinct().Count() == 1 ? offerings.First().StandardId : null, ReservedFor = "Admissions assessment" };
	}
	private static string Location(Room room) => room.RoomName;
	private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
	private static AdmissionAssessmentSessionDto Map(AdmissionAssessmentSession row, List<AdmissionAssessmentScopeDto> scopes, Dictionary<Guid, string> names)
	{
		var dto = AdmissionAssessmentSessionService.Map(row); dto.RoomReservationStatus = row.RoomReservationStatus; dto.InterviewerName = row.InterviewerId.HasValue ? names.GetValueOrDefault(row.InterviewerId.Value) : null;
		dto.ScopeDetails = scopes.Where(scope => row.Supports(scope.AdmissionOfferingId, scope.WorkflowStageDefinitionId)).ToList();
		dto.CanManage = row.Scopes.Count == 0 || dto.ScopeDetails.Count == row.Scopes.Count;
		dto.Scopes = dto.ScopeDetails.Select(scope => new AdmissionAssessmentSessionScopeInput { AdmissionOfferingId = scope.AdmissionOfferingId, WorkflowStageDefinitionId = scope.WorkflowStageDefinitionId }).ToList();
		if (dto.ScopeDetails.Count > 0) { dto.AdmissionOfferingId = dto.ScopeDetails[0].AdmissionOfferingId; dto.WorkflowStageDefinitionId = dto.ScopeDetails[0].WorkflowStageDefinitionId; }
		return dto;
	}
	private sealed record Plan(AdmissionAssessmentSession? Existing, Room Room, List<AdmissionOffering> Offerings, List<AdmissionAssessmentSessionScopeInput> Links, List<AdmissionAssessmentBookingCandidateDto> Candidates, Dictionary<Guid, List<(DateTime Start, DateTime End)>> Appointments, AdmissionAssessmentSessionPreviewDto Preview);
}
