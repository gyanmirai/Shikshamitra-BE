using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.Academic.Examination.SeatAllocation;
using ShikshaMitra.Academic.Timetable.RoomAvailability;
using ShikshaMitra.Academic.Timetable.Substitutions;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Academic.Audit;

[RemoteService(false)]
[Authorize]
public class AcademicAuditTrailAppService(
	IRepository<AcademicCalendarAuditLog, Guid> calendarAuditRepository,
	IRepository<AcademicCalendar, Guid> calendarRepository,
	IRepository<SubjectAuditLog, Guid> subjectAuditLogRepository,
	IRepository<ElectiveGroup, Guid> electiveGroupRepository,
	IRepository<RoomAuditLog, Guid> roomAuditRepository,
	IRepository<SubstitutionAuditLog, Guid> substitutionAuditRepository,
	IRepository<SubstitutionRequest, Guid> substitutionRepository,
	IRepository<SeatingPlanAuditLog, Guid> seatingPlanAuditRepository,
	IRepository<ExamResultAuditLog, Guid> examResultAuditRepository,
	IRepository<IdentityUser, Guid> identityUserRepository)
	: ApplicationService, IAcademicAuditTrailAppService
{
	private const int MaxSourceRows = 500;

	public async Task<PagedResultDto<AcademicAuditEventDto>> GetListAsync(AcademicAuditEventListRequestDto input)
	{
		await EnsureCanViewAcademicAuditAsync();

		var events = new List<AcademicAuditEventDto>();
		events.AddRange(await GetCalendarEventsAsync(input));
		events.AddRange(await GetSubjectEventsAsync(input));
		events.AddRange(await GetElectiveGroupEventsAsync(input));
		events.AddRange(await GetRoomEventsAsync(input));
		events.AddRange(await GetSubstitutionEventsAsync(input));
		events.AddRange(await GetSeatingPlanEventsAsync(input));
		events.AddRange(await GetResultEventsAsync(input));
		await EnrichUserReferencesAsync(events);

		var filtered = ApplyCommonFilters(events, input).ToList();
		var total = filtered.Count;
		var sorted = ApplySorting(filtered, input.Sorting)
			.Skip(input.SkipCount)
			.Take(NormalizeMaxResultCount(input.MaxResultCount))
			.ToList();

		return new PagedResultDto<AcademicAuditEventDto>(total, sorted);
	}

	private async Task EnrichUserReferencesAsync(List<AcademicAuditEventDto> events)
	{
		var userIds = events.Where(x => x.ActorUserId.HasValue).Select(x => x.ActorUserId!.Value)
			.Concat(events.SelectMany(x => x.ChangedFields)
				.Where(x => x.Field.EndsWith("UserId", StringComparison.OrdinalIgnoreCase)
					|| x.Field.EndsWith("CreatorId", StringComparison.OrdinalIgnoreCase)
					|| x.Field.EndsWith("ModifierId", StringComparison.OrdinalIgnoreCase))
				.SelectMany(x => new[] { x.OldValue, x.NewValue })
				.Where(x => Guid.TryParse(x, out _)).Select(Guid.Parse))
			.Distinct().ToList();
		if (userIds.Count == 0) return;

		var users = await identityUserRepository.GetListAsync(x => userIds.Contains(x.Id));
		var names = users.ToDictionary(x => x.Id, x =>
		{
			var fullName = string.Join(" ", new[] { x.Name, x.Surname }.Where(n => !string.IsNullOrWhiteSpace(n)));
			return string.IsNullOrWhiteSpace(fullName) ? x.UserName : fullName;
		});

		foreach (var auditEvent in events)
		{
			if (auditEvent.ActorUserId.HasValue && names.TryGetValue(auditEvent.ActorUserId.Value, out var actorName)
				&& (string.IsNullOrWhiteSpace(auditEvent.ActorNameSnapshot) || Guid.TryParse(auditEvent.ActorDisplayName, out _)))
				auditEvent.ActorDisplayName = actorName;

			foreach (var field in auditEvent.ChangedFields.Where(x => x.Field.EndsWith("UserId", StringComparison.OrdinalIgnoreCase)
				|| x.Field.EndsWith("CreatorId", StringComparison.OrdinalIgnoreCase)
				|| x.Field.EndsWith("ModifierId", StringComparison.OrdinalIgnoreCase)))
			{
				field.DisplayName = field.DisplayName.Replace(" Id", string.Empty, StringComparison.OrdinalIgnoreCase);
				if (Guid.TryParse(field.OldValue, out var oldId) && names.TryGetValue(oldId, out var oldName)) field.OldValue = oldName;
				if (Guid.TryParse(field.NewValue, out var newId) && names.TryGetValue(newId, out var newName)) field.NewValue = newName;
			}
		}
	}

	public async Task<AcademicAuditEventDto> GetAsync(Guid id)
	{
		var result = await GetListAsync(new AcademicAuditEventListRequestDto
		{
			SkipCount = 0,
			MaxResultCount = MaxSourceRows
		});
		var match = result.Items.FirstOrDefault(x => x.Id == id || x.SourceAuditLogId == id);
		if (match == null)
			throw new EntityNotFoundException(typeof(AcademicAuditEventDto), id);
		return match;
	}

	public async Task<PagedResultDto<AcademicAuditEventDto>> GetEntityEventsAsync(
		string entityType,
		Guid entityId,
		AcademicAuditEventListRequestDto input)
	{
		input.EntityType = entityType;
		input.EntityId = entityId;
		return await GetListAsync(input);
	}

	private async Task<List<AcademicAuditEventDto>> GetCalendarEventsAsync(AcademicAuditEventListRequestDto input)
	{
		var query = await calendarAuditRepository.GetQueryableAsync();
		if (input.FromDate.HasValue)
			query = query.Where(x => x.ChangedAt >= input.FromDate.Value);
		if (input.ToDate.HasValue)
			query = query.Where(x => x.ChangedAt <= input.ToDate.Value);

		var rows = await AsyncExecuter.ToListAsync(
			query.OrderByDescending(x => x.ChangedAt).Take(MaxSourceRows));

		var calendarIds = rows.Select(x => x.AcademicCalendarId).Distinct().ToList();
		var calendarQuery = await calendarRepository.GetQueryableAsync();
		var calendars = await AsyncExecuter.ToListAsync(calendarQuery.Where(x => calendarIds.Contains(x.Id)));
		var academicYearByCalendar = calendars.ToDictionary(x => x.Id, x => x.AcademicYearId);

		return rows.Select(x =>
		{
			academicYearByCalendar.TryGetValue(x.AcademicCalendarId, out var academicYearId);
			var entityId = x.AcademicCalendarDayId ?? x.AcademicCalendarId;
			var entityName = x.EntityName ?? (x.AcademicCalendarDayId.HasValue ? "Calendar day" : "Academic calendar");
			return AcademicAuditTrailProjection.Create(
				"academic_calendar_audit",
				"academic_calendar",
				x.EntityType ?? "calendar",
				entityId,
				entityName,
				x.Action.ToString(),
				x.ChangedAt,
				$"{ToLabel(x.Action.ToString())}: {entityName}",
				x.Id,
				academicYearId,
				x.ChangedByUserId,
				x.ActorNameSnapshot,
				x.ActorRoleSnapshot,
				x.Reason,
				x.ChangedFieldsJson,
				BuildJson(new { x.OldValueJson, x.NewValueJson, Metadata = new { x.IpAddress, x.Device, x.Browser, x.OperatingSystem, x.RequestPath, x.HttpMethod } }),
				x.CorrelationId);
		}).ToList();
	}

	private async Task<List<AcademicAuditEventDto>> GetSubjectEventsAsync(AcademicAuditEventListRequestDto input)
	{
		var query = await subjectAuditLogRepository.GetQueryableAsync();
		if (input.FromDate.HasValue)
			query = query.Where(x => x.ChangedAt >= input.FromDate.Value);
		if (input.ToDate.HasValue)
			query = query.Where(x => x.ChangedAt <= input.ToDate.Value);

		var rows = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.ChangedAt).Take(MaxSourceRows));

		return rows.Select(x => AcademicAuditTrailProjection.Create(
			"subject_audit",
			"subject_creation",
			"subject",
			x.SubjectId,
			x.EntityName ?? "Subject",
			x.Action,
			x.ChangedAt,
			$"{ToLabel(x.Action)} subject",
			x.Id,
			actorUserId: x.ChangedByUserId,
			actorName: x.ActorNameSnapshot,
			reason: x.Reason,
			changedFieldsJson: x.ChangedFieldsJson,
			contextJson: BuildJson(new { x.OldValueJson, x.NewValueJson, Metadata = new { x.IpAddress, x.Device, x.Browser, x.OperatingSystem, x.RequestPath, x.HttpMethod } }))).ToList();
	}

	private async Task<List<AcademicAuditEventDto>> GetElectiveGroupEventsAsync(AcademicAuditEventListRequestDto input)
	{
		var query = await electiveGroupRepository.GetQueryableAsync();
		query = query.Where(x => !x.IsDeleted);
		if (input.FromDate.HasValue)
			query = query.Where(x => x.CreationTime >= input.FromDate.Value || x.LastModificationTime >= input.FromDate.Value);
		if (input.ToDate.HasValue)
			query = query.Where(x => x.CreationTime <= input.ToDate.Value || x.LastModificationTime <= input.ToDate.Value);

		var rows = await AsyncExecuter.ToListAsync(
			query.OrderByDescending(x => x.LastModificationTime ?? x.CreationTime).Take(MaxSourceRows));

		return rows.SelectMany(x => CreateAuditedEntityEvents(
			"elective_group_entity",
			"elective_group_creation",
			"elective_group",
			x.Id,
			x.Name,
			x.CreationTime,
			x.CreatorId,
			x.LastModificationTime,
			x.LastModifierId,
			x.ArchiveReason,
			BuildJson(new { x.Name, x.GroupType, x.Description, x.Status, x.SortOrder, x.AcademicMediumId }))).ToList();
	}

	private async Task<List<AcademicAuditEventDto>> GetRoomEventsAsync(AcademicAuditEventListRequestDto input)
	{
		var query = await roomAuditRepository.GetQueryableAsync();
		if (input.FromDate.HasValue)
			query = query.Where(x => x.Timestamp >= input.FromDate.Value);
		if (input.ToDate.HasValue)
			query = query.Where(x => x.Timestamp <= input.ToDate.Value);

		var rows = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.Timestamp).Take(MaxSourceRows));
		return rows.Select(x => AcademicAuditTrailProjection.Create(
			"room_audit",
			"room_availability",
			x.ReservationId.HasValue ? "room_reservation" : "room",
			x.ReservationId ?? x.RoomId,
			x.ReservationId.HasValue ? "Room reservation" : "Room",
			x.Action,
			x.Timestamp,
			$"{x.Action} room availability record",
			x.Id,
			actorUserId: x.PerformedByUserId,
			actorName: x.UserName,
			reason: x.Remarks,
			contextJson: BuildJson(new { x.RoomId, x.ReservationId, x.OldValue, x.NewValue, x.Remarks }))).ToList();
	}

	private async Task<List<AcademicAuditEventDto>> GetSubstitutionEventsAsync(AcademicAuditEventListRequestDto input)
	{
		var query = await substitutionAuditRepository.GetQueryableAsync();
		if (input.FromDate.HasValue)
			query = query.Where(x => x.Timestamp >= input.FromDate.Value);
		if (input.ToDate.HasValue)
			query = query.Where(x => x.Timestamp <= input.ToDate.Value);

		var rows = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.Timestamp).Take(MaxSourceRows));
		var requestIds = rows.Select(x => x.SubstitutionRequestId).Distinct().ToList();
		var substitutionQuery = await substitutionRepository.GetQueryableAsync();
		var substitutions = await AsyncExecuter.ToListAsync(substitutionQuery.Where(x => requestIds.Contains(x.Id)));
		var academicYearByRequest = substitutions.ToDictionary(x => x.Id, x => x.AcademicYearId);

		return rows.Select(x =>
		{
			academicYearByRequest.TryGetValue(x.SubstitutionRequestId, out var academicYearId);
			return AcademicAuditTrailProjection.Create(
				"substitution_audit",
				"substitution_management",
				"substitution_request",
				x.SubstitutionRequestId,
				"Substitution request",
				x.Action,
				x.Timestamp,
				$"{x.Action} substitution request",
				x.Id,
				academicYearId,
				x.ActorUserId,
				x.ActorName,
				contextJson: x.Details);
		}).ToList();
	}

	private async Task<List<AcademicAuditEventDto>> GetSeatingPlanEventsAsync(AcademicAuditEventListRequestDto input)
	{
		var query = await seatingPlanAuditRepository.GetQueryableAsync();
		if (input.FromDate.HasValue)
			query = query.Where(x => x.ChangedAt >= input.FromDate.Value);
		if (input.ToDate.HasValue)
			query = query.Where(x => x.ChangedAt <= input.ToDate.Value);

		var rows = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.ChangedAt).Take(MaxSourceRows));
		return rows.Select(x => AcademicAuditTrailProjection.Create(
			"seating_plan_audit",
			"seating_plan",
			"exam",
			x.ExamId,
			"Exam seating plan",
			x.Action.ToString(),
			x.ChangedAt,
			$"{ToLabel(x.Action.ToString())} seating plan",
			x.Id,
			actorUserId: x.ActorUserId,
			actorName: x.ActorNameSnapshot,
			reason: x.Reason,
			contextJson: x.AssignmentsSnapshotJson)).ToList();
	}

	private async Task<List<AcademicAuditEventDto>> GetResultEventsAsync(AcademicAuditEventListRequestDto input)
	{
		var query = await examResultAuditRepository.GetQueryableAsync();
		if (input.FromDate.HasValue)
			query = query.Where(x => x.CreationTime >= input.FromDate.Value);
		if (input.ToDate.HasValue)
			query = query.Where(x => x.CreationTime <= input.ToDate.Value);

		var rows = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.CreationTime).Take(MaxSourceRows));
		return rows.Select(x => AcademicAuditTrailProjection.Create(
			"exam_result_audit",
			"result_creation",
			"exam_result",
			x.ExamId,
			"Exam result",
			x.Action,
			x.CreationTime,
			$"{x.Action} exam result",
			x.Id,
			actorUserId: x.PerformedById,
			reason: x.Remarks,
			contextJson: BuildJson(new { x.ExamId, x.DivisionId, x.Remarks }))).ToList();
	}

	private static IEnumerable<AcademicAuditEventDto> CreateAuditedEntityEvents(
		string source,
		string subModule,
		string entityType,
		Guid entityId,
		string entityName,
		DateTime creationTime,
		Guid? creatorId,
		DateTime? lastModificationTime,
		Guid? lastModifierId,
		string? reason,
		string contextJson)
	{
		yield return AcademicAuditTrailProjection.Create(
			source,
			subModule,
			entityType,
			entityId,
			entityName,
			"created",
			creationTime,
			$"Created {entityName}",
			academicYearId: null,
			actorUserId: creatorId,
			contextJson: contextJson);

		if (lastModificationTime.HasValue)
		{
			yield return AcademicAuditTrailProjection.Create(
				source,
				subModule,
				entityType,
				entityId,
				entityName,
				"updated",
				lastModificationTime.Value,
				$"Updated {entityName}",
				academicYearId: null,
				actorUserId: lastModifierId,
				reason: reason,
				contextJson: contextJson);
		}
	}

	private async Task EnsureCanViewAcademicAuditAsync()
	{
		if (await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Academics.Audits.View)
			|| await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Academics.Default)
			|| await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Academics.Dashboard.View))
			return;

		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Academics.Audits.View);
	}

	private static IEnumerable<AcademicAuditEventDto> ApplyCommonFilters(
		IEnumerable<AcademicAuditEventDto> query,
		AcademicAuditEventListRequestDto input)
	{
		if (input.AcademicYearId.HasValue)
			query = query.Where(x => x.AcademicYearId == null || x.AcademicYearId == input.AcademicYearId.Value);
		if (!string.IsNullOrWhiteSpace(input.SubModuleCode))
			query = query.Where(x => x.SubModuleCode == Normalize(input.SubModuleCode));
		if (!string.IsNullOrWhiteSpace(input.EntityType))
			query = query.Where(x => x.EntityType == Normalize(input.EntityType));
		if (input.EntityId.HasValue)
			query = query.Where(x => x.EntityId == input.EntityId.Value);
		if (!string.IsNullOrWhiteSpace(input.Action))
			query = query.Where(x => x.Action == Normalize(input.Action));
		if (input.ActorUserId.HasValue)
			query = query.Where(x => x.ActorUserId == input.ActorUserId.Value);
		if (!string.IsNullOrWhiteSpace(input.CorrelationId))
			query = query.Where(x => x.CorrelationId == input.CorrelationId.Trim());
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLowerInvariant();
			query = query.Where(x =>
				x.EntityName.ToLowerInvariant().Contains(search) ||
				x.Summary.ToLowerInvariant().Contains(search) ||
				x.SubModuleDisplayName.ToLowerInvariant().Contains(search) ||
				x.ActionLabel.ToLowerInvariant().Contains(search) ||
				(x.ActorNameSnapshot != null && x.ActorNameSnapshot.ToLowerInvariant().Contains(search)) ||
				(x.Reason != null && x.Reason.ToLowerInvariant().Contains(search)) ||
				(x.ContextJson != null && x.ContextJson.ToLowerInvariant().Contains(search)) ||
				(x.CorrelationId != null && x.CorrelationId.ToLowerInvariant().Contains(search)));
		}
		return query;
	}

	private static IEnumerable<AcademicAuditEventDto> ApplySorting(
		IEnumerable<AcademicAuditEventDto> query,
		string? sorting)
	{
		var sort = string.IsNullOrWhiteSpace(sorting) ? "occurredAt desc" : sorting.Trim();
		var descending = !sort.EndsWith(" asc", StringComparison.OrdinalIgnoreCase);
		if (sort.StartsWith("action", StringComparison.OrdinalIgnoreCase))
			return descending ? query.OrderByDescending(x => x.Action) : query.OrderBy(x => x.Action);
		if (sort.StartsWith("subModule", StringComparison.OrdinalIgnoreCase))
			return descending ? query.OrderByDescending(x => x.SubModuleDisplayName) : query.OrderBy(x => x.SubModuleDisplayName);
		return descending
			? query.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
			: query.OrderBy(x => x.OccurredAt).ThenBy(x => x.Id);
	}

	private static int NormalizeMaxResultCount(int maxResultCount) =>
		maxResultCount <= 0 ? 25 : Math.Min(maxResultCount, 100);

	private static string Normalize(string value) => value.Trim().ToLowerInvariant().Replace("-", "_");

	private static string ToLabel(string value) =>
		System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
			System.Text.RegularExpressions.Regex.Replace(value.Trim(), "([a-z])([A-Z])", "$1 $2")
				.Replace("_", " ").Replace("-", " ").ToLowerInvariant());

	private static string BuildJson<T>(T value) => JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
}
