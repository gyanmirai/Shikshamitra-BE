using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Audit;

[RemoteService(false)]
[Authorize]
public class HrAuditTrailAppService(IRepository<HrAuditEvent, Guid> repository)
	: ApplicationService, IHrAuditTrailAppService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private static readonly string[] PolicyAuditEntityTypes =
	[
		"work_calendar_policy",
		"payroll_policy",
		"attendance_policy",
		"policy_assignment",
		"policy_version"
	];
	private static readonly string[] AttendanceAuditEntityTypes = ["employee_attendance"];
	private static readonly string[] PayrollAuditEntityTypes = ["payroll_run"];

	public async Task<PagedResultDto<HrAuditEventDto>> GetListAsync(HrAuditEventListRequestDto input)
	{
		var allowedEntityTypes = await ResolveAllowedEntityTypesAsync(input.EntityType);
		var query = await repository.GetQueryableAsync();
		if (allowedEntityTypes is not null)
		{
			query = query.Where(x => allowedEntityTypes.Contains(x.EntityType));
		}
		query = ApplyFilters(query, input);
		var total = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(ApplySorting(query, input.Sorting)
			.Skip(input.SkipCount)
			.Take(NormalizeMaxResultCount(input.MaxResultCount)));
		return new PagedResultDto<HrAuditEventDto>(total, rows.Select(ToDto).ToList());
	}

	public async Task<PagedResultDto<HrAuditEventDto>> GetEntityEventsAsync(string entityType, Guid entityId, HrAuditEventListRequestDto input)
	{
		input.EntityType = entityType;
		input.EntityId = entityId;
		return await GetListAsync(input);
	}

	private async Task<string[]?> ResolveAllowedEntityTypesAsync(string? entityType)
	{
		if (!string.IsNullOrWhiteSpace(entityType))
		{
			await AuthorizationService.CheckAsync(ResolveAuditPermission(entityType));
			return null;
		}

		var allowed = new List<string>();
		if (await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.Policies.ViewAudit))
		{
			allowed.AddRange(PolicyAuditEntityTypes);
		}
		if (await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_Evaluations.ViewAudit))
		{
			allowed.AddRange(AttendanceAuditEntityTypes);
		}
		if (await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.ViewAudit))
		{
			allowed.AddRange(PayrollAuditEntityTypes);
		}
		return allowed.Distinct().ToArray();
	}

	private static string ResolveAuditPermission(string? entityType)
	{
		return Normalize(entityType ?? string.Empty) switch
		{
			"payroll_run" => ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.ViewAudit,
			"employee_attendance" => ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_Evaluations.ViewAudit,
			_ => ShikshaMitraSubmodulePermissions.HR.Policies.ViewAudit
		};
	}

	private static IQueryable<HrAuditEvent> ApplyFilters(IQueryable<HrAuditEvent> query, HrAuditEventListRequestDto input)
	{
		if (input.AcademicYearId.HasValue)
		{
			query = query.Where(x => x.AcademicYearId == input.AcademicYearId.Value);
		}
		if (!string.IsNullOrWhiteSpace(input.EntityType))
		{
			var entityType = Normalize(input.EntityType);
			query = query.Where(x => x.EntityType == entityType);
		}
		if (input.EntityId.HasValue)
		{
			query = query.Where(x => x.EntityId == input.EntityId.Value);
		}
		if (!string.IsNullOrWhiteSpace(input.Action))
		{
			var action = Normalize(input.Action);
			query = query.Where(x => x.Action == action);
		}
		if (input.ActorUserId.HasValue)
		{
			query = query.Where(x => x.ActorUserId == input.ActorUserId.Value);
		}
		if (input.FromDate.HasValue)
		{
			query = query.Where(x => x.OccurredAt >= input.FromDate.Value);
		}
		if (input.ToDate.HasValue)
		{
			query = query.Where(x => x.OccurredAt <= input.ToDate.Value);
		}
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLowerInvariant();
			query = query.Where(x =>
				x.EntityName.ToLower().Contains(search) ||
				x.Summary.ToLower().Contains(search) ||
				(x.ActorNameSnapshot != null && x.ActorNameSnapshot.ToLower().Contains(search)));
		}
		return query;
	}

	private static IOrderedQueryable<HrAuditEvent> ApplySorting(IQueryable<HrAuditEvent> query, string? sorting)
	{
		var sort = ParseSorting(sorting);
		return sort.Field switch
		{
			"action" => sort.Descending
				? query.OrderByDescending(x => x.Action).ThenByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
				: query.OrderBy(x => x.Action).ThenByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id),
			"actorNameSnapshot" => sort.Descending
				? query.OrderByDescending(x => x.ActorNameSnapshot).ThenByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
				: query.OrderBy(x => x.ActorNameSnapshot).ThenByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id),
			_ => sort.Descending
				? query.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
				: query.OrderBy(x => x.OccurredAt).ThenByDescending(x => x.Id)
		};
	}

	private static (string Field, bool Descending) ParseSorting(string? sorting)
	{
		if (string.IsNullOrWhiteSpace(sorting))
		{
			return ("occurredAt", true);
		}

		var parts = sorting.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		var requestedField = parts.Length > 0 ? parts[0] : "occurredAt";
		var field = requestedField switch
		{
			var name when name.Equals("occurredAt", StringComparison.OrdinalIgnoreCase) => "occurredAt",
			var name when name.Equals("action", StringComparison.OrdinalIgnoreCase) => "action",
			var name when name.Equals("actorNameSnapshot", StringComparison.OrdinalIgnoreCase) => "actorNameSnapshot",
			_ => "occurredAt"
		};
		var descending = parts.Length < 2 || !parts[1].Equals("asc", StringComparison.OrdinalIgnoreCase);
		return (field, descending);
	}

	private static int NormalizeMaxResultCount(int maxResultCount) => maxResultCount <= 0 ? 25 : Math.Min(maxResultCount, 100);

	private static HrAuditEventDto ToDto(HrAuditEvent entity)
	{
		var changedFields = DeserializeChangedFields(entity.ChangedFieldsJson);
		return new()
		{
			Id = entity.Id,
			AcademicYearId = entity.AcademicYearId,
			EntityType = entity.EntityType,
			EntityId = entity.EntityId,
			EntityName = entity.EntityName,
			Action = entity.Action,
			ActorUserId = entity.ActorUserId,
			ActorNameSnapshot = entity.ActorNameSnapshot,
			OccurredAt = entity.OccurredAt,
			OccurredAtUtc = NormalizeUtc(entity.OccurredAt),
			OccurredAtDisplay = string.Empty,
			ActorDisplayName = entity.ActorNameSnapshot ?? entity.ActorUserId?.ToString() ?? "System",
			ActionLabel = ToLabel(entity.Action),
			Reason = entity.Reason,
			Summary = entity.Summary,
			ChangedFields = changedFields,
			ChangedFieldCount = changedFields.Count,
			ContextJson = entity.ContextJson,
			CorrelationId = entity.CorrelationId,
			SourceAuditLogId = entity.SourceAuditLogId
		};
	}

	private static List<HrAuditChangedFieldDto> DeserializeChangedFields(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return [];
		}
		try
		{
			return JsonSerializer.Deserialize<List<HrAuditChangedFieldDto>>(json, JsonOptions) ?? [];
		}
		catch (JsonException)
		{
			return [];
		}
	}

	private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
	{
		DateTimeKind.Utc => value,
		DateTimeKind.Local => value.ToUniversalTime(),
		_ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
	};

	private static string ToLabel(string value) =>
		CultureInfo.InvariantCulture.TextInfo.ToTitleCase(Normalize(value).Replace("_", " "));

	private static string Normalize(string value) => value.Trim().ToLowerInvariant().Replace("-", "_");
}
