using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionApplicationDecisionHistory : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ApplicationId { get; private set; }
	public AdmissionApplicationStatus Status { get; private set; }
	public string? Reason { get; private set; }
	public Guid? ActorId { get; private set; }
	public DateTime OccurredAt { get; private set; }
	public string? Action { get; private set; }
	public Guid? StageInstanceId { get; private set; }
	public string? IdempotencyKey { get; private set; }
	public string? MetadataJson { get; private set; }

	protected AdmissionApplicationDecisionHistory() { }

	public AdmissionApplicationDecisionHistory(
		Guid id,
		Guid? tenantId,
		Guid applicationId,
		AdmissionApplicationStatus status,
		string? reason,
		Guid? actorId,
		string? action = null,
		Guid? stageInstanceId = null,
		string? idempotencyKey = null,
		string? metadataJson = null) : base(id)
	{
		TenantId = tenantId;
		ApplicationId = Check.NotDefaultOrNull<Guid>(applicationId, nameof(applicationId));
		Status = status;
		Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
		ActorId = actorId;
		OccurredAt = DateTime.UtcNow;
		Action = Optional(action, 32, nameof(action));
		StageInstanceId = stageInstanceId;
		IdempotencyKey = Optional(idempotencyKey, 80, nameof(idempotencyKey));
		MetadataJson = metadataJson;
		IsDeleted = false;
	}

	private static string? Optional(string? value, int max, string name)
	{
		if (string.IsNullOrWhiteSpace(value)) return null;
		return Check.Length(value.Trim(), name, max);
	}
}
