using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionReviewDecision : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ApplicationId { get; private set; }
	public Guid StageInstanceId { get; private set; }
	public Guid RecordId { get; private set; }
	public string Decision { get; private set; } = string.Empty;
	public string? Reason { get; private set; }
	public Guid DecidedBy { get; private set; }
	public DateTime DecidedAt { get; private set; }
	private AdmissionReviewDecision() { }
	public AdmissionReviewDecision(Guid id, Guid? tenantId, Guid applicationId, Guid stageId, Guid recordId,
		string decision, string? reason, Guid actor, DateTime now) : base(id)
	{
		if (decision is not ("approve" or "waitlist" or "reject" or "requestCorrection") || reason?.Length > 1024 ||
			decision != "approve" && string.IsNullOrWhiteSpace(reason) || actor == Guid.Empty)
			throw new BusinessException(AdmissionReviewErrorCodes.InvalidInput);
		TenantId = tenantId; ApplicationId = applicationId; StageInstanceId = stageId; RecordId = recordId;
		Decision = decision; Reason = reason?.Trim(); DecidedBy = actor; DecidedAt = now.ToUniversalTime();
	}
}
