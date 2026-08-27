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

	protected AdmissionApplicationDecisionHistory() { }

	public AdmissionApplicationDecisionHistory(
		Guid id,
		Guid? tenantId,
		Guid applicationId,
		AdmissionApplicationStatus status,
		string? reason,
		Guid? actorId) : base(id)
	{
		TenantId = tenantId;
		ApplicationId = Check.NotDefaultOrNull<Guid>(applicationId, nameof(applicationId));
		Status = status;
		Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
		ActorId = actorId;
		OccurredAt = DateTime.UtcNow;
		IsDeleted = false;
	}
}
