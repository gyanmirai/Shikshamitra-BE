using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionApplication : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string ApplicationNumber { get; private set; }
	public Guid ApplicantId { get; private set; }
	public Guid? InquiryId { get; private set; }
	public Guid AdmissionCycleId { get; private set; }
	public Guid AdmissionOfferingId { get; private set; }
	public Guid WorkflowDefinitionId { get; private set; }
	public Guid FormDefinitionId { get; private set; }
	public AdmissionApplicationStatus Status { get; private set; }
	public Guid? CurrentStageInstanceId { get; private set; }
	public DateTime? LastSavedAt { get; private set; }
	public DateTime? SubmittedAt { get; private set; }
	protected AdmissionApplication() { }

	public AdmissionApplication(Guid id, Guid? tenantId, string applicationNumber, Guid applicantId, Guid? inquiryId,
		Guid cycleId, Guid offeringId, Guid workflowDefinitionId, Guid formDefinitionId) : base(id)
	{
		TenantId = tenantId; ApplicationNumber = Check.NotNullOrWhiteSpace(applicationNumber, nameof(applicationNumber)); ApplicantId = applicantId;
		InquiryId = inquiryId; AdmissionCycleId = cycleId; AdmissionOfferingId = offeringId; WorkflowDefinitionId = workflowDefinitionId;
		FormDefinitionId = formDefinitionId; Status = AdmissionApplicationStatus.Draft; LastSavedAt = DateTime.UtcNow;
	}

	public void SetCurrentStage(Guid stageInstanceId) => CurrentStageInstanceId = stageInstanceId;
	public void SaveDraft() { if (Status != AdmissionApplicationStatus.Draft) throw new BusinessException(AdmissionApplicationErrorCodes.AlreadySubmitted); LastSavedAt = DateTime.UtcNow; }
	public void Submit() { if (Status != AdmissionApplicationStatus.Draft) throw new BusinessException(AdmissionApplicationErrorCodes.AlreadySubmitted); Status = AdmissionApplicationStatus.Submitted; SubmittedAt = DateTime.UtcNow; LastSavedAt = SubmittedAt; }
}
