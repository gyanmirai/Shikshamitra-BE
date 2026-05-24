using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Audit;

[RemoteService(false)]
[Authorize]
public class HrAuditTrailAppService(IRepository<HrAuditEvent, Guid> repository)
	: ApplicationService, IHrAuditTrailAppService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public async Task<PagedResultDto<HrAuditEventDto>> GetListAsync(HrAuditEventListRequestDto input)
	{
		await EnsureAuditPermissionAsync(input.EntityType);
		var query = await repository.GetQueryableAsync();
		query = ApplyFilters(query, input);
		var total = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query
			.OrderByDescending(x => x.OccurredAt)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount <= 0 ? 25 : input.MaxResultCount));
		return new PagedResultDto<HrAuditEventDto>(total, rows.Select(ToDto).ToList());
	}

	public async Task<PagedResultDto<HrAuditEventDto>> GetEntityEventsAsync(string entityType, Guid entityId, HrAuditEventListRequestDto input)
	{
		input.EntityType = entityType;
		input.EntityId = entityId;
		return await GetListAsync(input);
	}

	private async Task EnsureAuditPermissionAsync(string? entityType)
	{
		await AuthorizationService.CheckAsync(ResolveAuditPermission(entityType));
	}

	private static string ResolveAuditPermission(string? entityType)
	{
		return Normalize(entityType ?? string.Empty) switch
		{
			"payroll_run" => ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.ViewAudit,
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

	private static HrAuditEventDto ToDto(HrAuditEvent entity) => new()
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
		Reason = entity.Reason,
		Summary = entity.Summary,
		ChangedFields = DeserializeChangedFields(entity.ChangedFieldsJson),
		ContextJson = entity.ContextJson,
		CorrelationId = entity.CorrelationId,
		SourceAuditLogId = entity.SourceAuditLogId
	};

	private static List<HrAuditChangedFieldDto> DeserializeChangedFields(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return [];
		}
		return JsonSerializer.Deserialize<List<HrAuditChangedFieldDto>>(json, JsonOptions) ?? [];
	}

	private static string Normalize(string value) => value.Trim().ToLowerInvariant().Replace("-", "_");
}
