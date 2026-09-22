using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionCycleHistory : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AdmissionCycleId { get; private set; }
	public AdmissionCycleHistoryAction Action { get; private set; }
	public AdmissionCycleStatus? FromStatus { get; private set; }
	public AdmissionCycleStatus? ToStatus { get; private set; }
	public string Description { get; private set; } = string.Empty;
	public string? Reason { get; private set; }
	public DateTime OccurredAt { get; private set; }

	private AdmissionCycleHistory() { }

	public AdmissionCycleHistory(Guid id, Guid? tenantId, Guid admissionCycleId,
		AdmissionCycleHistoryAction action, AdmissionCycleStatus? fromStatus, AdmissionCycleStatus? toStatus,
		string description, string? reason, DateTime occurredAt) : base(id)
	{
		TenantId = tenantId;
		AdmissionCycleId = Check.NotDefaultOrNull<Guid>(admissionCycleId, nameof(admissionCycleId));
		Action = action;
		FromStatus = fromStatus;
		ToStatus = toStatus;
		Description = Check.NotNullOrWhiteSpace(description, nameof(description), AdmissionCycleConsts.MaxDescriptionLength).Trim();
		Reason = string.IsNullOrWhiteSpace(reason)
			? null
			: Check.Length(reason.Trim(), nameof(reason), AdmissionCycleConsts.MaxReasonLength);
		OccurredAt = occurredAt.ToUniversalTime();
	}
}
