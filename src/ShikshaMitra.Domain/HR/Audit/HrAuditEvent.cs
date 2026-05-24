using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Audit;

public class HrAuditEvent : AggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }
	public Guid? AcademicYearId { get; protected set; }
	public string EntityType { get; protected set; } = string.Empty;
	public Guid EntityId { get; protected set; }
	public string EntityName { get; protected set; } = string.Empty;
	public string Action { get; protected set; } = string.Empty;
	public Guid? ActorUserId { get; protected set; }
	public string? ActorNameSnapshot { get; protected set; }
	public DateTime OccurredAt { get; protected set; }
	public string? Reason { get; protected set; }
	public string Summary { get; protected set; } = string.Empty;
	public string? ChangedFieldsJson { get; protected set; }
	public string? ContextJson { get; protected set; }
	public string? CorrelationId { get; protected set; }
	public Guid? SourceAuditLogId { get; protected set; }

	protected HrAuditEvent()
	{
	}

	public HrAuditEvent(
		Guid id,
		Guid? tenantId,
		Guid? academicYearId,
		string entityType,
		Guid entityId,
		string entityName,
		string action,
		Guid? actorUserId,
		string? actorNameSnapshot,
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
		EntityType = Normalize(entityType);
		EntityId = entityId;
		EntityName = Trim(entityName, 256);
		Action = Normalize(action);
		ActorUserId = actorUserId;
		ActorNameSnapshot = TrimNullable(actorNameSnapshot, 128);
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
