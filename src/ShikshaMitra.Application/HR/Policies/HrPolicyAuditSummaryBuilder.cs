using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Audit;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.AuditLogging;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyAuditSummaryBuilder(
	IRepository<HrPolicyAssignment, Guid> assignmentRepository,
	IRepository<WorkCalendarPolicy, Guid> workCalendarPolicyRepository,
	IRepository<PayrollPolicy, Guid> payrollPolicyRepository,
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	IRepository<IdentityUser, Guid> userRepository,
	IRepository<AuditLog, Guid> auditLogRepository,
	IRepository<HrAuditEvent, Guid> hrAuditEventRepository,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	public async Task<HrPolicyAuditSummaryDto> BuildAsync(string policyType, Guid id)
	{
		var normalized = HrPolicyImpactPreviewBuilder.NormalizePolicyType(policyType);
		if (normalized == "work_calendar")
		{
			var entity = await workCalendarPolicyRepository.GetAsync(id);
			return await BuildAuditSummaryAsync(normalized, id, entity.Name, entity.Version, entity);
		}
		if (normalized == "payroll")
		{
			var entity = await payrollPolicyRepository.GetAsync(id);
			var summary = await BuildAuditSummaryAsync(normalized, id, entity.Name, entity.Version, entity);
			summary.LatestAssignmentReason = await GetLatestAssignmentReasonAsync(x => x.PayrollPolicyId == id);
			return summary;
		}
		if (normalized == "attendance")
		{
			var entity = await attendancePolicyRepository.GetAsync(id);
			var summary = await BuildAuditSummaryAsync(normalized, id, entity.Name, entity.Version, entity);
			summary.LatestAssignmentReason = await GetLatestAssignmentReasonAsync(x => x.AttendancePolicyId == id);
			return summary;
		}

		throw new BusinessException(HRErrorCodes.HrPolicyInvalidType);
	}

	private async Task<string?> GetLatestAssignmentReasonAsync(System.Linq.Expressions.Expression<Func<HrPolicyAssignment, bool>> predicate)
	{
		var query = await assignmentRepository.GetQueryableAsync();
		return (await asyncExecuter.FirstOrDefaultAsync(query
			.Where(predicate)
			.OrderByDescending(x => x.LastModificationTime ?? x.CreationTime)))?.AssignmentReason;
	}

	private async Task<HrPolicyAuditSummaryDto> BuildAuditSummaryAsync(
		string policyType,
		Guid id,
		string name,
		int version,
		FullAuditedAggregateRoot<Guid> entity)
	{
		var userIds = new[] { entity.CreatorId, entity.LastModifierId }
			.Where(x => x.HasValue)
			.Select(x => x!.Value)
			.Distinct()
			.ToList();
		var users = userIds.Count == 0
			? new Dictionary<Guid, string>()
			: (await asyncExecuter.ToListAsync((await userRepository.GetQueryableAsync()).Where(x => userIds.Contains(x.Id))))
				.ToDictionary(x => x.Id, x => x.Name ?? x.UserName);
		var events = await BuildAuditEventsAsync(id, policyType, entity);
		return new HrPolicyAuditSummaryDto
		{
			PolicyType = policyType,
			PolicyId = id,
			Name = name,
			Version = version,
			CreationTime = entity.CreationTime,
			CreatorId = entity.CreatorId,
			CreatedByName = entity.CreatorId.HasValue ? users.GetValueOrDefault(entity.CreatorId.Value) : null,
			LastModificationTime = entity.LastModificationTime,
			LastModifierId = entity.LastModifierId,
			LastModifiedByName = entity.LastModifierId.HasValue ? users.GetValueOrDefault(entity.LastModifierId.Value) : null,
			LastDefaultSetSummary = "Default changes are available in the ABP audit log.",
			Events = events
		};
	}

	private async Task<List<HrPolicyAuditEventDto>> BuildAuditEventsAsync(
		Guid id,
		string policyType,
		FullAuditedAggregateRoot<Guid> entity)
	{
		var entityType = policyType switch
		{
			"work_calendar" => "work_calendar_policy",
			"payroll" => "payroll_policy",
			"attendance" => "attendance_policy",
			_ => policyType
		};
		var hrAuditQuery = await hrAuditEventRepository.GetQueryableAsync();
		var hrAuditEvents = await asyncExecuter.ToListAsync(hrAuditQuery
			.Where(x => x.EntityType == entityType && x.EntityId == id)
			.OrderByDescending(x => x.OccurredAt)
			.Take(50));
		if (hrAuditEvents.Count > 0)
		{
			return hrAuditEvents.Select(x => new HrPolicyAuditEventDto
			{
				Timestamp = x.OccurredAt,
				ActorId = x.ActorUserId,
				ActorName = x.ActorNameSnapshot,
				Action = x.Action,
				SourceMethod = "HrAuditEvent",
				ChangedFieldsSummary = BuildHrAuditChangedFieldsSummary(x)
			}).ToList();
		}

		var query = await auditLogRepository.GetQueryableAsync();
		var logs = await asyncExecuter.ToListAsync(query
			.Where(x => x.ExecutionTime >= entity.CreationTime.AddMinutes(-1))
			.OrderByDescending(x => x.ExecutionTime)
			.Take(50));
		var userIds = logs.Where(x => x.UserId.HasValue).Select(x => x.UserId!.Value).Distinct().ToList();
		var users = userIds.Count == 0
			? new Dictionary<Guid, string>()
			: (await asyncExecuter.ToListAsync((await userRepository.GetQueryableAsync()).Where(x => userIds.Contains(x.Id))))
				.ToDictionary(x => x.Id, x => x.Name ?? x.UserName);
		var events = logs
			.SelectMany(log => (log.Actions ?? []).Select(action => new { log, action }))
			.Where(x => IsPolicyAuditAction(x.action.MethodName))
			.Take(20)
			.Select(x => new HrPolicyAuditEventDto
			{
				Timestamp = x.log.ExecutionTime,
				ActorId = x.log.UserId,
				ActorName = x.log.UserId.HasValue ? users.GetValueOrDefault(x.log.UserId.Value) : null,
				Action = MapAuditAction(x.action.MethodName),
				SourceMethod = x.action.MethodName,
				ChangedFieldsSummary = $"Policy {id:D}"
			})
			.ToList();
		events.Add(new HrPolicyAuditEventDto
		{
			Timestamp = entity.CreationTime,
			ActorId = entity.CreatorId,
			ActorName = entity.CreatorId.HasValue ? users.GetValueOrDefault(entity.CreatorId.Value) : null,
			Action = "created",
			ChangedFieldsSummary = $"Created {policyType} policy"
		});
		return events.OrderByDescending(x => x.Timestamp).ToList();
	}

	private static string? BuildHrAuditChangedFieldsSummary(HrAuditEvent auditEvent)
	{
		if (string.IsNullOrWhiteSpace(auditEvent.ChangedFieldsJson))
		{
			return auditEvent.Summary;
		}

		try
		{
			var fields = System.Text.Json.JsonSerializer.Deserialize<List<HrAuditChangedFieldDto>>(auditEvent.ChangedFieldsJson, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) ?? [];
			return fields.Count == 0
				? auditEvent.Summary
				: string.Join(", ", fields.Take(6).Select(x => $"{x.Field}: {x.OldValue} -> {x.NewValue}"));
		}
		catch
		{
			return auditEvent.Summary;
		}
	}

	private static bool IsPolicyAuditAction(string? methodName)
	{
		if (string.IsNullOrWhiteSpace(methodName))
		{
			return false;
		}

		var method = methodName.ToLowerInvariant();
		return method.Contains("policy") && (method.Contains("update") || method.Contains("clone") || method.Contains("delete") || method.Contains("setdefault") || method.Contains("create"));
	}

	private static string MapAuditAction(string? methodName)
	{
		var method = methodName?.ToLowerInvariant() ?? string.Empty;
		if (method.Contains("clone")) return "cloned";
		if (method.Contains("delete")) return "archived";
		if (method.Contains("setdefault")) return "set_default";
		if (method.Contains("update")) return "edited";
		if (method.Contains("create")) return "created";
		return "changed";
	}
}
