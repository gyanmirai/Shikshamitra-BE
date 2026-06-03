using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Audit;

public class FinanceAuditEvent : AggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }
	public Guid? AcademicYearId { get; protected set; }
	public string SubModuleCode { get; protected set; } = string.Empty;
	public string EntityType { get; protected set; } = string.Empty;
	public Guid EntityId { get; protected set; }
	public string EntityName { get; protected set; } = string.Empty;
	public string Action { get; protected set; } = string.Empty;
	public Guid? StudentId { get; protected set; }
	public string? StudentNameSnapshot { get; protected set; }
	public string? StudentGrNoSnapshot { get; protected set; }
	public string? StudentRollNoSnapshot { get; protected set; }
	public Guid? ActorUserId { get; protected set; }
	public string? ActorNameSnapshot { get; protected set; }
	public string? PerformedByRoleSnapshot { get; protected set; }
	public DateTime OccurredAt { get; protected set; }
	public string? Reason { get; protected set; }
	public string Summary { get; protected set; } = string.Empty;
	public string? ChangedFieldsJson { get; protected set; }
	public string? ContextJson { get; protected set; }
	public string? CorrelationId { get; protected set; }
	public Guid? SourceAuditLogId { get; protected set; }

	protected FinanceAuditEvent()
	{
	}

	public FinanceAuditEvent(
		Guid id,
		Guid? tenantId,
		Guid? academicYearId,
		string subModuleCode,
		string entityType,
		Guid entityId,
		string entityName,
		string action,
		Guid? studentId,
		string? studentNameSnapshot,
		string? studentGrNoSnapshot,
		string? studentRollNoSnapshot,
		Guid? actorUserId,
		string? actorNameSnapshot,
		string? performedByRoleSnapshot,
		DateTime occurredAt,
		string summary,
		string? reason = null,
		string? changedFieldsJson = null,
		string? contextJson = null,
		string? correlationId = null,
		Guid? sourceAuditLogId = null) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		SubModuleCode = Normalize(subModuleCode);
		EntityType = Normalize(entityType);
		EntityId = entityId;
		EntityName = Trim(entityName, 256);
		Action = Normalize(action);
		StudentId = studentId;
		StudentNameSnapshot = TrimNullable(studentNameSnapshot, 256);
		StudentGrNoSnapshot = TrimNullable(studentGrNoSnapshot, 32);
		StudentRollNoSnapshot = TrimNullable(studentRollNoSnapshot, 32);
		ActorUserId = actorUserId;
		ActorNameSnapshot = TrimNullable(actorNameSnapshot, 128);
		PerformedByRoleSnapshot = TrimNullable(performedByRoleSnapshot, 128);
		OccurredAt = occurredAt;
		Reason = TrimNullable(reason, 512);
		Summary = Trim(summary, 512);
		ChangedFieldsJson = changedFieldsJson;
		ContextJson = contextJson;
		CorrelationId = TrimNullable(correlationId, 128);
		SourceAuditLogId = sourceAuditLogId;
	}

	private static string Normalize(string value) => value.Trim().ToLowerInvariant().Replace("-", "_");
	private static string Trim(string value, int max) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim()[..Math.Min(value.Trim().Length, max)];
	private static string? TrimNullable(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : Trim(value, max);
}
