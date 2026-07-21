using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPlatformSupportSession : FullAuditedAggregateRoot<Guid>
{
	public Guid OperatorUserId { get; private set; }
	public Guid TargetTenantId { get; private set; }
	public string Reason { get; private set; } = string.Empty;
	public string? Reference { get; private set; }
	public DateTime ExpiresAt { get; private set; }
	public DateTime? EndedAt { get; private set; }

	protected AdmissionCrmPlatformSupportSession() { }

	public AdmissionCrmPlatformSupportSession(Guid id, Guid operatorUserId, Guid targetTenantId, string reason, string? reference, DateTime expiresAt) : base(id)
	{
		OperatorUserId = operatorUserId;
		TargetTenantId = targetTenantId;
		Reason = Check.NotNullOrWhiteSpace(reason, nameof(reason), 500);
		Reference = string.IsNullOrWhiteSpace(reference) ? null : Check.Length(reference.Trim(), nameof(reference), 100);
		ExpiresAt = expiresAt;
	}

	public bool IsActive(DateTime now) => EndedAt is null && ExpiresAt > now;
	public void End(DateTime now) => EndedAt ??= now;
}
