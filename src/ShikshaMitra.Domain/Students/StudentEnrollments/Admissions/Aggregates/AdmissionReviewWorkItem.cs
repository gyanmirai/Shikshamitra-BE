using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionReviewWorkItem : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ApplicationId { get; private set; }
	public Guid StageInstanceId { get; private set; }
	public Guid? AssignedReviewerId { get; private set; }
	public DateTime? DueAt { get; private set; }
	public string Status { get; private set; } = "notStarted";
	private AdmissionReviewWorkItem() { }
	public AdmissionReviewWorkItem(Guid id, Guid? tenantId, Guid applicationId, Guid stageId, Guid? assignee, DateTime? dueAt) : base(id)
	{
		TenantId = tenantId; ApplicationId = applicationId; StageInstanceId = stageId; Assign(assignee, dueAt);
	}
	public void Assign(Guid? assignee, DateTime? dueAt)
	{
		if (assignee == Guid.Empty) throw new BusinessException(AdmissionReviewErrorCodes.InvalidInput);
		AssignedReviewerId = assignee; DueAt = dueAt?.ToUniversalTime();
	}
	public void Record(string status)
	{
		if (status is not ("notStarted" or "draft" or "submitted" or "approved" or "waitlisted" or "rejected" or "correctionRequested"))
			throw new BusinessException(AdmissionReviewErrorCodes.InvalidInput);
		Status = status;
	}
}
