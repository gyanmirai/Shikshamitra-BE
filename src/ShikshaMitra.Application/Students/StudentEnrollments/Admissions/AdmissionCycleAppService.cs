using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionCycleAppService(
	IRepository<AdmissionCycle, Guid> cycleRepository,
	IRepository<AdmissionCycleHistory, Guid> historyRepository,
	IRepository<AdmissionOffering, Guid> offeringRepository,
	IRepository<AdmissionApplication, Guid> applicationRepository,
	IRepository<AdmissionSeatPool, Guid> seatPoolRepository,
	IRepository<AdmissionSeatReservation, Guid> seatReservationRepository,
	IRepository<AdmissionWorkflowStageDefinition, Guid> workflowStageRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	StudentScopeAccessService studentScope,
	AdmissionApplicationScopeAccessService applicationScope) : ApplicationService, IAdmissionCycleAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.View)]
	public async Task<PagedResultDto<AdmissionCycleDto>> GetListAsync(AdmissionCycleListInput input)
	{
		var today = AdmissionCycleWindowEvaluator.GetInstitutionDate(Clock.Now);
		var queryable = await cycleRepository.GetQueryableAsync();
		var scope = await studentScope.GetCurrentUserAcademicYearScopeAsync();
		queryable = queryable.Where(cycle => cycle.TenantId == CurrentTenant.Id);
		if (!scope.HasAllAcademicYears)
			queryable = queryable.Where(cycle => scope.AcademicYearIds.Contains(cycle.AcademicYearId));
		queryable = queryable.Where(x =>
			(string.IsNullOrWhiteSpace(input.Search) || x.Name.Contains(input.Search) || x.Code.Contains(input.Search)) &&
			(!input.AcademicYearId.HasValue || x.AcademicYearId == input.AcademicYearId.Value) &&
			(!input.Status.HasValue || x.Status == input.Status.Value));

		queryable = ApplyWindowFilter(queryable, input.WindowStatus, today);
		var totalCount = await AsyncExecuter.CountAsync(queryable);
		var rows = await AsyncExecuter.ToListAsync(ApplySorting(queryable, input.Sorting)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount));

		var context = await LoadMapContextAsync(rows);
		return new PagedResultDto<AdmissionCycleDto>(totalCount, rows.Select(x => Map(x, context)).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.View)]
	public async Task<AdmissionCycleDto> GetAsync(Guid id)
	{
		var cycle = await GetCycleAsync(id, requireYearWideScope: false);
		return Map(cycle, await LoadMapContextAsync([cycle]));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	public async Task<AdmissionCycleDto> CreateAsync(CreateUpdateAdmissionCycleDto input)
	{
		await EnsureAcademicYearExistsAsync(input.AcademicYearId);
		var id = GuidGenerator.Create();
		var code = AdmissionSettingsCode.ForCreate(input.Code, "CYCLE", id);
		await EnsureCodeIsUniqueAsync(code);

		var cycle = new AdmissionCycle(id, CurrentTenant.Id, input.Name, code,
			input.AcademicYearId, input.StartDate, input.EndDate, input.Description);
		await cycleRepository.InsertAsync(cycle, autoSave: false);
		await RecordHistoryAsync(cycle, AdmissionCycleHistoryAction.Created, null, cycle.Status,
			"Admission cycle draft created.");
		await CurrentUnitOfWork.SaveChangesAsync();
		return Map(cycle, await LoadMapContextAsync([cycle]));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	public async Task<AdmissionCycleDto> UpdateAsync(Guid id, CreateUpdateAdmissionCycleDto input)
	{
		var cycle = await GetCycleAsync(id);
		EnsureConcurrency(cycle, input.ConcurrencyStamp);
		await EnsureAcademicYearExistsAsync(input.AcademicYearId);
		var code = AdmissionSettingsCode.ForUpdate(input.Code, cycle.Code);
		await EnsureCodeIsUniqueAsync(code, id);
		cycle.UpdateDetails(input.Name, code, input.AcademicYearId, input.StartDate, input.EndDate,
			input.Description);
		await cycleRepository.UpdateAsync(cycle, autoSave: false);
		await RecordHistoryAsync(cycle, AdmissionCycleHistoryAction.Updated, cycle.Status, cycle.Status,
			"Admission cycle draft details updated.");
		await CurrentUnitOfWork.SaveChangesAsync();
		return Map(cycle, await LoadMapContextAsync([cycle]));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	public async Task<AdmissionCycleDto> PublishAsync(Guid id, string? concurrencyStamp = null)
	{
		var cycle = await GetCycleAsync(id);
		EnsureConcurrency(cycle, concurrencyStamp);
		var validation = await ValidateCycleAsync(cycle);
		if (!validation.IsValid)
		{
			throw new BusinessException(AdmissionErrorCodes.CycleCannotBeModified,
				string.Join(" ", validation.Errors.Select(x => x.Message)));
		}

		var previous = cycle.Status;
		cycle.Publish(CurrentUser.Id, Clock.Now);
		await cycleRepository.UpdateAsync(cycle, autoSave: false);
		await RecordHistoryAsync(cycle, AdmissionCycleHistoryAction.Published, previous, cycle.Status,
			"Admission cycle published.");
		await CurrentUnitOfWork.SaveChangesAsync();
		return Map(cycle, await LoadMapContextAsync([cycle]));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Delete)]
	public async Task<AdmissionCycleDto> CloseAsync(Guid id, AdmissionCycleActionDto input)
	{
		var cycle = await GetCycleAsync(id);
		EnsureConcurrency(cycle, input.ConcurrencyStamp);
		EnsureReason(input.Reason);
		var impact = await GetImpactInternalAsync(id);
		if (impact.RequiresActiveOfferingConfirmation && !input.ConfirmActiveOfferings)
		{
			throw new BusinessException(AdmissionErrorCodes.CycleHasActiveOfferings)
				.WithData("ActiveOfferingCount", impact.ActiveOfferingCount);
		}

		var previous = cycle.Status;
		cycle.Close(CurrentUser.Id, Clock.Now);
		await cycleRepository.UpdateAsync(cycle, autoSave: false);
		await RecordHistoryAsync(cycle, AdmissionCycleHistoryAction.Closed, previous, cycle.Status,
			"Admission cycle closed. New applications are no longer accepted.", input.Reason);
		await CurrentUnitOfWork.SaveChangesAsync();
		return Map(cycle, await LoadMapContextAsync([cycle]));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	public async Task<AdmissionCycleDto> ReopenAsync(Guid id, ReopenAdmissionCycleDto input)
	{
		var cycle = await GetCycleAsync(id);
		EnsureConcurrency(cycle, input.ConcurrencyStamp);
		EnsureReason(input.Reason);
		var today = AdmissionCycleWindowEvaluator.GetInstitutionDate(Clock.Now);
		if (input.NewEndDate.HasValue)
		{
			await EnsureOfferingsFitWindowAsync(id, cycle.StartDate, input.NewEndDate.Value);
			cycle.ReviseWindow(cycle.StartDate, input.NewEndDate.Value);
		}
		if (cycle.EndDate.Date < today)
		{
			throw new BusinessException(AdmissionErrorCodes.CycleReopenDateExpired)
				.WithData("CurrentEndDate", cycle.EndDate.Date);
		}

		var previous = cycle.Status;
		cycle.Reopen(CurrentUser.Id, Clock.Now);
		await cycleRepository.UpdateAsync(cycle, autoSave: false);
		await RecordHistoryAsync(cycle, AdmissionCycleHistoryAction.Reopened, previous, cycle.Status,
			"Admission cycle reopened.", input.Reason);
		await CurrentUnitOfWork.SaveChangesAsync();
		return Map(cycle, await LoadMapContextAsync([cycle]));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Delete)]
	public async Task<AdmissionCycleDto> ArchiveAsync(Guid id, AdmissionCycleActionDto input)
	{
		var cycle = await GetCycleAsync(id);
		EnsureConcurrency(cycle, input.ConcurrencyStamp);
		EnsureReason(input.Reason);
		var impact = await GetImpactInternalAsync(id);
		if (impact.ActiveOfferingCount > 0)
		{
			throw new BusinessException(AdmissionErrorCodes.CycleHasActiveOfferings)
				.WithData("ActiveOfferingCount", impact.ActiveOfferingCount);
		}

		var previous = cycle.Status;
		cycle.Archive(CurrentUser.Id, Clock.Now);
		await cycleRepository.UpdateAsync(cycle, autoSave: false);
		await RecordHistoryAsync(cycle, AdmissionCycleHistoryAction.Archived, previous, cycle.Status,
			"Admission cycle archived.", input.Reason);
		await CurrentUnitOfWork.SaveChangesAsync();
		return Map(cycle, await LoadMapContextAsync([cycle]));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Delete)]
	public async Task DeleteAsync(Guid id, string? concurrencyStamp = null)
	{
		var cycle = await GetCycleAsync(id);
		EnsureConcurrency(cycle, concurrencyStamp);
		var impact = await GetImpactInternalAsync(id);
		if (cycle.Status != AdmissionCycleStatus.Draft || impact.LinkedOfferingCount > 0 || impact.ApplicationCount > 0)
		{
			throw new BusinessException(AdmissionErrorCodes.CycleDeleteNotAllowed);
		}
		await cycleRepository.DeleteAsync(cycle, autoSave: true);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	public async Task<AdmissionCycleDto> CloneAsync(CloneAdmissionCycleDto input)
	{
		var source = await GetCycleAsync(input.SourceCycleId);
		await EnsureAcademicYearExistsAsync(input.AcademicYearId);
		var cloneId = GuidGenerator.Create();
		var code = AdmissionSettingsCode.ForCreate(input.Code, "CYCLE", cloneId);
		await EnsureCodeIsUniqueAsync(code);
		var clone = new AdmissionCycle(cloneId, CurrentTenant.Id, input.Name, code,
			input.AcademicYearId, input.StartDate, input.EndDate, input.Description ?? source.Description);
		await cycleRepository.InsertAsync(clone, autoSave: false);
		await RecordHistoryAsync(clone, AdmissionCycleHistoryAction.Cloned, null, clone.Status,
			$"Admission cycle cloned from {source.Code}.");
		await CurrentUnitOfWork.SaveChangesAsync();
		return Map(clone, await LoadMapContextAsync([clone]));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	public async Task<AdmissionCycleDto> UpdateWindowAsync(Guid id, UpdateAdmissionCycleWindowDto input)
	{
		var cycle = await GetCycleAsync(id);
		EnsureConcurrency(cycle, input.ConcurrencyStamp);
		EnsureReason(input.Reason);
		await EnsureOfferingsFitWindowAsync(id, input.StartDate, input.EndDate);
		cycle.ReviseWindow(input.StartDate, input.EndDate);
		await cycleRepository.UpdateAsync(cycle, autoSave: false);
		await RecordHistoryAsync(cycle, AdmissionCycleHistoryAction.Updated, cycle.Status, cycle.Status,
			"Published admission window revised.", input.Reason);
		await CurrentUnitOfWork.SaveChangesAsync();
		return Map(cycle, await LoadMapContextAsync([cycle]));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	public async Task<AdmissionCycleDto> ScheduleAsync(Guid id, ScheduleAdmissionCycleDto input)
	{
		var cycle = await GetCycleAsync(id);
		EnsureConcurrency(cycle, input.ConcurrencyStamp);
		var now = Clock.Now.ToUniversalTime();
		if ((input.PublishAt.HasValue && input.PublishAt.Value.ToUniversalTime() <= now) ||
			(input.CloseAt.HasValue && input.CloseAt.Value.ToUniversalTime() <= now))
		{
			throw new BusinessException(AdmissionErrorCodes.CycleScheduleInvalid);
		}
		if (input.PublishAt.HasValue)
		{
			var validation = await ValidateCycleAsync(cycle);
			var publishDate = AdmissionCycleWindowEvaluator.GetInstitutionDate(input.PublishAt.Value);
			if (!validation.IsValid || publishDate > cycle.EndDate.Date)
			{
				throw new BusinessException(AdmissionErrorCodes.CycleScheduleInvalid,
					"The cycle must be publish-ready and its scheduled publish time must fall on or before the application close date.");
			}
		}
		cycle.SetSchedule(input.PublishAt, input.CloseAt);
		await cycleRepository.UpdateAsync(cycle, autoSave: false);
		await RecordHistoryAsync(cycle, AdmissionCycleHistoryAction.ScheduleChanged, cycle.Status, cycle.Status,
			"Admission cycle publication or closure schedule updated.");
		await CurrentUnitOfWork.SaveChangesAsync();
		return Map(cycle, await LoadMapContextAsync([cycle]));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	public async Task<AdmissionCycleValidationDto> ValidateAsync(Guid id) =>
		await ValidateCycleAsync(await GetCycleAsync(id));

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.View)]
	public async Task<AdmissionCycleImpactDto> GetImpactAsync(Guid id)
	{
		await GetCycleAsync(id);
		return await GetImpactInternalAsync(id);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.View)]
	public async Task<List<AdmissionCycleHistoryDto>> GetHistoryAsync(Guid id)
	{
		await GetCycleAsync(id);
		var rows = await historyRepository.GetListAsync(x => x.AdmissionCycleId == id);
		return rows.OrderByDescending(x => x.OccurredAt).Select(x => new AdmissionCycleHistoryDto
		{
			Id = x.Id,
			AdmissionCycleId = x.AdmissionCycleId,
			Action = x.Action,
			FromStatus = x.FromStatus,
			ToStatus = x.ToStatus,
			Description = x.Description,
			Reason = x.Reason,
			OccurredAt = x.OccurredAt,
			CreationTime = x.CreationTime,
			CreatorId = x.CreatorId
		}).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.View)]
	public async Task<AdmissionCycleCapacitySummaryDto> GetCapacityAsync(Guid id)
	{
		await GetCycleAsync(id);
		return await BuildCapacitySummaryAsync(id);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	public async Task<AdmissionCycleCapacitySummaryDto> UpdateOfferingCapacityAsync(Guid id, Guid offeringId,
		UpdateAdmissionCycleOfferingCapacityDto input)
	{
		var cycle = await GetCycleAsync(id);
		if (cycle.Status is AdmissionCycleStatus.Closed or AdmissionCycleStatus.Archived)
			throw new BusinessException(AdmissionErrorCodes.CycleCannotBeModified,
				"Seat capacity can only be changed while the admission cycle is draft or published.");

		var offering = await offeringRepository.FindAsync(offeringId);
		if (offering == null || offering.AdmissionCycleId != id)
			throw new EntityNotFoundException(typeof(AdmissionOffering), offeringId);
		if (!offering.WorkflowDefinitionId.HasValue ||
			await workflowStageRepository.FindAsync(x => x.WorkflowDefinitionId == offering.WorkflowDefinitionId.Value &&
				x.StageType == AdmissionWorkflowStageType.Seat) == null)
			throw new BusinessException(AdmissionErrorCodes.OfferingConfigurationIncomplete,
				"Assign a published admission process with a Seat stage before configuring capacity.");

		var reason = input.Reason.Trim();
		var pool = await seatPoolRepository.FindAsync(x => x.AdmissionOfferingId == offeringId);
		var previousCapacity = pool?.Capacity;
		if (pool == null)
		{
			if (!string.IsNullOrWhiteSpace(input.PoolConcurrencyStamp))
				throw new BusinessException(AdmissionErrorCodes.CycleConcurrencyConflict,
					"Seat capacity was configured after this view was opened. Refresh and try again.");
			pool = new AdmissionSeatPool(GuidGenerator.Create(), CurrentTenant.Id, offeringId, input.Capacity);
			await seatPoolRepository.InsertAsync(pool, autoSave: false);
		}
		else
		{
			EnsurePoolConcurrency(pool, input.PoolConcurrencyStamp);
			await ReconcileExpiredReservationsAsync(pool);
			pool.SetCapacity(input.Capacity, allowBelowOccupied: false);
			await seatPoolRepository.UpdateAsync(pool, autoSave: false);
		}

		await RecordHistoryAsync(cycle, AdmissionCycleHistoryAction.Updated, cycle.Status, cycle.Status,
			previousCapacity.HasValue
				? $"Seat capacity for {offering.Code} changed from {previousCapacity.Value} to {input.Capacity}."
				: $"Seat capacity for {offering.Code} set to {input.Capacity}.", reason);
		try { await CurrentUnitOfWork.SaveChangesAsync(); }
		catch (AbpDbConcurrencyException)
		{
			throw new BusinessException(AdmissionErrorCodes.CycleConcurrencyConflict,
				"Seat capacity changed after this view was opened. Refresh and try again.");
		}
		return await BuildCapacitySummaryAsync(id);
	}

	private async Task<AdmissionCycleCapacitySummaryDto> BuildCapacitySummaryAsync(Guid id)
	{
		var offerings = (await offeringRepository.GetListAsync(x => x.AdmissionCycleId == id))
			.OrderByDescending(x => x.IsActive).ThenBy(x => x.Name).ToList();
		var offeringIds = offerings.Select(x => x.Id).ToHashSet();
		var workflowIds = offerings.Where(x => x.WorkflowDefinitionId.HasValue)
			.Select(x => x.WorkflowDefinitionId!.Value).ToHashSet();
		var seatStages = workflowIds.Count == 0
			? []
			: await workflowStageRepository.GetListAsync(x => workflowIds.Contains(x.WorkflowDefinitionId) &&
				x.StageType == AdmissionWorkflowStageType.Seat);
		var seatManagedWorkflowIds = seatStages.Select(x => x.WorkflowDefinitionId).ToHashSet();
		var pools = offeringIds.Count == 0
			? []
			: await seatPoolRepository.GetListAsync(x => offeringIds.Contains(x.AdmissionOfferingId));
		var poolsByOffering = pools.ToDictionary(x => x.AdmissionOfferingId);
		var poolIds = pools.Select(x => x.Id).ToHashSet();
		var reservations = poolIds.Count == 0
			? []
			: await seatReservationRepository.GetListAsync(x => poolIds.Contains(x.PoolId));
		var now = Clock.Now;
		var rows = offerings.Select(offering =>
		{
			var isSeatManaged = offering.WorkflowDefinitionId.HasValue &&
				seatManagedWorkflowIds.Contains(offering.WorkflowDefinitionId.Value);
			if (!poolsByOffering.TryGetValue(offering.Id, out var pool))
				return new AdmissionCycleOfferingCapacityDto
				{
					AdmissionOfferingId = offering.Id,
					OfferingName = offering.Name,
					OfferingCode = offering.Code,
					IsActive = offering.IsActive,
					IsSeatManaged = isSeatManaged
				};
			var poolReservations = reservations.Where(x => x.PoolId == pool.Id).ToList();
			var reserved = poolReservations.Count(x => x.Status == AdmissionSeatReservationStatus.Reserved && x.ExpiresAt > now);
			var confirmed = poolReservations.Count(x => x.Status == AdmissionSeatReservationStatus.Confirmed);
			var waitlisted = poolReservations.Count(x => x.Status == AdmissionSeatReservationStatus.Waitlisted);
			var occupied = reserved + confirmed;
			return new AdmissionCycleOfferingCapacityDto
			{
				AdmissionOfferingId = offering.Id,
				OfferingName = offering.Name,
				OfferingCode = offering.Code,
				IsActive = offering.IsActive,
				IsSeatManaged = isSeatManaged,
				IsConfigured = true,
				Capacity = pool.Capacity,
				ReservedCount = reserved,
				ConfirmedCount = confirmed,
				WaitlistedCount = waitlisted,
				RemainingCapacity = Math.Max(0, pool.Capacity - occupied),
				IsOverCapacity = occupied > pool.Capacity,
				PoolConcurrencyStamp = pool.ConcurrencyStamp
			};
		}).ToList();
		var seatManagedRows = rows.Where(x => x.IsSeatManaged).ToList();
		return new AdmissionCycleCapacitySummaryDto
		{
			AdmissionCycleId = id,
			OfferingCount = offerings.Count,
			ActiveOfferingCount = offerings.Count(x => x.IsActive),
			SeatManagedOfferingCount = seatManagedRows.Count,
			ConfiguredOfferingCount = seatManagedRows.Count(x => x.IsConfigured),
			UnconfiguredOfferingCount = seatManagedRows.Count(x => !x.IsConfigured),
			TotalCapacity = seatManagedRows.Sum(x => x.Capacity ?? 0),
			ReservedCount = seatManagedRows.Sum(x => x.ReservedCount),
			ConfirmedCount = seatManagedRows.Sum(x => x.ConfirmedCount),
			WaitlistedCount = seatManagedRows.Sum(x => x.WaitlistedCount),
			RemainingCapacity = seatManagedRows.Sum(x => x.RemainingCapacity ?? 0),
			OverCapacityOfferingCount = seatManagedRows.Count(x => x.IsOverCapacity),
			Offerings = rows
		};
	}

	private async Task ReconcileExpiredReservationsAsync(AdmissionSeatPool pool)
	{
		var expired = await seatReservationRepository.GetListAsync(x => x.PoolId == pool.Id &&
			x.Status == AdmissionSeatReservationStatus.Reserved && x.ExpiresAt <= Clock.Now);
		if (expired.Count == 0) return;
		pool.ExpireReserved(expired.Count);
		foreach (var reservation in expired)
		{
			reservation.Expire(Clock.Now);
			await seatReservationRepository.UpdateAsync(reservation, autoSave: false);
		}
	}

	private async Task<AdmissionCycleValidationDto> ValidateCycleAsync(AdmissionCycle cycle)
	{
		var result = new AdmissionCycleValidationDto();
		if (cycle.Status != AdmissionCycleStatus.Draft)
		{
			result.Errors.Add(Issue("CycleStatus", "Only a draft admission cycle can be published."));
		}
		if (cycle.EndDate.Date <= cycle.StartDate.Date)
		{
			result.Errors.Add(Issue("DateRange", "Applications close date must be after the open date."));
		}
		if (await academicYearRepository.FindAsync(cycle.AcademicYearId) == null)
		{
			result.Errors.Add(Issue("AcademicYear", "The selected Academic Year is no longer available."));
		}
		var today = AdmissionCycleWindowEvaluator.GetInstitutionDate(Clock.Now);
		if (cycle.EndDate.Date < today)
		{
			result.Errors.Add(Issue("WindowEnded", "The application window has already ended. Extend the window before publishing."));
		}
		return result;
	}

	private async Task<AdmissionCycleImpactDto> GetImpactInternalAsync(Guid id)
	{
		var offerings = await offeringRepository.GetListAsync(x => x.AdmissionCycleId == id);
		var applications = await applicationRepository.GetListAsync(x => x.AdmissionCycleId == id);
		return new AdmissionCycleImpactDto
		{
			AdmissionCycleId = id,
			LinkedOfferingCount = offerings.Count,
			ActiveOfferingCount = offerings.Count(x => x.IsActive),
			ApplicationCount = applications.Count,
			DraftApplicationCount = applications.Count(x => x.Status == AdmissionApplicationStatus.Draft),
			SubmittedApplicationCount = applications.Count(x => x.Status == AdmissionApplicationStatus.Submitted)
		};
	}

	private async Task EnsureOfferingsFitWindowAsync(Guid id, DateTime startDate, DateTime endDate)
	{
		if (endDate.Date <= startDate.Date)
		{
			throw new BusinessException(AdmissionErrorCodes.CycleDateRangeInvalid);
		}
		var offerings = await offeringRepository.GetListAsync(x => x.AdmissionCycleId == id);
		if (offerings.Any(x =>
			(x.OpenDate.HasValue && x.OpenDate.Value.Date < startDate.Date) ||
			(x.CloseDate.HasValue && x.CloseDate.Value.Date > endDate.Date)))
		{
			throw new BusinessException(AdmissionErrorCodes.OfferingDateRangeInvalid,
				"Existing Admission Offering dates must remain inside the revised cycle window.");
		}
	}

	private async Task<AdmissionCycleMapContext> LoadMapContextAsync(IReadOnlyCollection<AdmissionCycle> cycles)
	{
		var cycleIds = cycles.Select(x => x.Id).ToHashSet();
		var yearIds = cycles.Select(x => x.AcademicYearId).ToHashSet();
		var years = yearIds.Count == 0
			? []
			: await academicYearRepository.GetListAsync(x => yearIds.Contains(x.Id));
		var scopedOfferings = await applicationScope.GetScopedOfferingsAsync();
		var offerings = cycleIds.Count == 0
			? []
			: await AsyncExecuter.ToListAsync(scopedOfferings.Where(x => cycleIds.Contains(x.AdmissionCycleId)));
		var applications = cycleIds.Count == 0
			? []
			: await AsyncExecuter.ToListAsync((await applicationScope.GetScopedApplicationsAsync()).Where(x =>
				cycleIds.Contains(x.AdmissionCycleId) && scopedOfferings.Any(offering => offering.Id == x.AdmissionOfferingId)));

		return new AdmissionCycleMapContext(
			years.ToDictionary(x => x.Id, x => x.Name),
			offerings.GroupBy(x => x.AdmissionCycleId).ToDictionary(x => x.Key, x => x.Count()),
			offerings.Where(x => x.IsActive).GroupBy(x => x.AdmissionCycleId).ToDictionary(x => x.Key, x => x.Count()),
			applications.GroupBy(x => x.AdmissionCycleId).ToDictionary(x => x.Key, x => x.Count()),
			AdmissionCycleWindowEvaluator.GetInstitutionDate(Clock.Now));
	}

	private static AdmissionCycleDto Map(AdmissionCycle cycle, AdmissionCycleMapContext context)
	{
		var windowStatus = AdmissionCycleWindowEvaluator.EvaluateForDate(cycle.Status, cycle.StartDate,
			cycle.EndDate, context.Today);
		return new AdmissionCycleDto
		{
			ConcurrencyStamp = cycle.ConcurrencyStamp,
			Id = cycle.Id,
			Name = cycle.Name,
			Code = cycle.Code,
			Description = cycle.Description,
			AcademicYearId = cycle.AcademicYearId,
			AcademicYearName = context.AcademicYears.GetValueOrDefault(cycle.AcademicYearId, string.Empty),
			StartDate = cycle.StartDate,
			EndDate = cycle.EndDate,
			Status = cycle.Status,
			WindowStatus = windowStatus,
			IsAcceptingApplications = cycle.Status == AdmissionCycleStatus.Published &&
				windowStatus == AdmissionCycleWindowStatus.Open,
			PublishedAt = cycle.PublishedAt,
			PublishedBy = cycle.PublishedBy,
			ClosedAt = cycle.ClosedAt,
			ClosedBy = cycle.ClosedBy,
			ReopenedAt = cycle.ReopenedAt,
			ReopenedBy = cycle.ReopenedBy,
			ArchivedAt = cycle.ArchivedAt,
			ArchivedBy = cycle.ArchivedBy,
			ScheduledPublishAt = cycle.ScheduledPublishAt,
			ScheduledCloseAt = cycle.ScheduledCloseAt,
			LinkedOfferingCount = context.OfferingCounts.GetValueOrDefault(cycle.Id),
			ActiveOfferingCount = context.ActiveOfferingCounts.GetValueOrDefault(cycle.Id),
			ApplicationCount = context.ApplicationCounts.GetValueOrDefault(cycle.Id),
			CreationTime = cycle.CreationTime,
			CreatorId = cycle.CreatorId,
			LastModificationTime = cycle.LastModificationTime,
			LastModifierId = cycle.LastModifierId
		};
	}

	private async Task<AdmissionCycle> GetCycleAsync(Guid id, bool requireYearWideScope = true)
	{
		var cycle = await cycleRepository.FindAsync(x => x.Id == id && x.TenantId == CurrentTenant.Id)
			?? throw new EntityNotFoundException(typeof(AdmissionCycle), id);
		await studentScope.EnsureCanAccessAcademicYearAsync(cycle.AcademicYearId, "AdmissionCycle");
		if (requireYearWideScope && !await studentScope.CurrentUserHasAcademicYearWideScopeAsync(cycle.AcademicYearId))
			throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		return cycle;
	}

	private async Task EnsureAcademicYearExistsAsync(Guid id)
	{
		if (id == Guid.Empty || await academicYearRepository.FindAsync(id) == null)
		{
			throw new EntityNotFoundException(typeof(AcademicYear), id);
		}
		if (!await studentScope.CurrentUserHasAcademicYearWideScopeAsync(id))
			throw new BusinessException(UserScopeErrorCodes.AccessDenied);
	}

	private async Task EnsureCodeIsUniqueAsync(string code, Guid? excludingId = null)
	{
		var normalized = code.Trim().ToUpperInvariant();
		var queryable = await cycleRepository.GetQueryableAsync();
		var exists = await AsyncExecuter.AnyAsync(queryable.Where(x => x.Code == normalized &&
			(!excludingId.HasValue || x.Id != excludingId.Value)));
		if (exists)
		{
			throw new BusinessException(AdmissionErrorCodes.CycleCodeAlreadyExists).WithData("Code", normalized);
		}
	}

	private async Task RecordHistoryAsync(AdmissionCycle cycle, AdmissionCycleHistoryAction action,
		AdmissionCycleStatus? fromStatus, AdmissionCycleStatus? toStatus, string description, string? reason = null) =>
		await historyRepository.InsertAsync(new AdmissionCycleHistory(GuidGenerator.Create(), CurrentTenant.Id,
			cycle.Id, action, fromStatus, toStatus, description, reason, Clock.Now), autoSave: false);

	private static void EnsureConcurrency(AdmissionCycle cycle, string? stamp)
	{
		if (string.IsNullOrWhiteSpace(stamp) || !string.Equals(cycle.ConcurrencyStamp, stamp, StringComparison.Ordinal))
		{
			throw new BusinessException(AdmissionErrorCodes.CycleConcurrencyConflict,
				"The admission cycle changed after it was opened. Reload and try again.");
		}
	}

	private static void EnsurePoolConcurrency(AdmissionSeatPool pool, string? stamp)
	{
		if (string.IsNullOrWhiteSpace(stamp) || !string.Equals(pool.ConcurrencyStamp, stamp, StringComparison.Ordinal))
		{
			throw new BusinessException(AdmissionErrorCodes.CycleConcurrencyConflict,
				"Seat capacity changed after this view was opened. Refresh and try again.");
		}
	}

	private static void EnsureReason(string? reason)
	{
		if (string.IsNullOrWhiteSpace(reason))
		{
			throw new BusinessException(AdmissionErrorCodes.CycleReasonRequired,
				"A reason is required for this admission cycle action.");
		}
	}

	private static AdmissionCycleValidationIssueDto Issue(string code, string message) => new()
	{
		Code = code,
		Message = message
	};

	private static IQueryable<AdmissionCycle> ApplyWindowFilter(IQueryable<AdmissionCycle> query,
		AdmissionCycleWindowStatus? status, DateTime today) => status switch
	{
		AdmissionCycleWindowStatus.Upcoming => query.Where(x =>
			x.Status != AdmissionCycleStatus.Closed && x.Status != AdmissionCycleStatus.Archived && x.StartDate > today),
		AdmissionCycleWindowStatus.Open => query.Where(x =>
			x.Status != AdmissionCycleStatus.Closed && x.Status != AdmissionCycleStatus.Archived &&
			x.StartDate <= today && x.EndDate >= today),
		AdmissionCycleWindowStatus.Ended => query.Where(x =>
			x.Status != AdmissionCycleStatus.Closed && x.Status != AdmissionCycleStatus.Archived && x.EndDate < today),
		AdmissionCycleWindowStatus.Closed => query.Where(x => x.Status == AdmissionCycleStatus.Closed),
		AdmissionCycleWindowStatus.Archived => query.Where(x => x.Status == AdmissionCycleStatus.Archived),
		_ => query
	};

	private static IQueryable<AdmissionCycle> ApplySorting(IQueryable<AdmissionCycle> query, string? sorting) =>
		sorting?.Trim().ToLowerInvariant() switch
		{
			"name" => query.OrderBy(x => x.Name),
			"name desc" => query.OrderByDescending(x => x.Name),
			"code" => query.OrderBy(x => x.Code),
			"code desc" => query.OrderByDescending(x => x.Code),
			"startdate" => query.OrderBy(x => x.StartDate),
			"startdate desc" => query.OrderByDescending(x => x.StartDate),
			"enddate" => query.OrderBy(x => x.EndDate),
			"enddate desc" => query.OrderByDescending(x => x.EndDate),
			"status" => query.OrderBy(x => x.Status),
			"status desc" => query.OrderByDescending(x => x.Status),
			_ => query.OrderByDescending(x => x.CreationTime)
		};

	private sealed record AdmissionCycleMapContext(
		IReadOnlyDictionary<Guid, string> AcademicYears,
		IReadOnlyDictionary<Guid, int> OfferingCounts,
		IReadOnlyDictionary<Guid, int> ActiveOfferingCounts,
		IReadOnlyDictionary<Guid, int> ApplicationCounts,
		DateTime Today);
}
