using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmDocumentChecklistAssignment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid LeadId { get; private set; }
	public Guid RegistrationApplicationId { get; private set; }
	public Guid ChecklistTemplateId { get; private set; }
	public int ChecklistTemplateVersion { get; private set; }
	public string TemplateSnapshotJson { get; private set; } = "{}";
	public DateTime AssignedAt { get; private set; }
	public DateTime? ReadinessConfirmedAt { get; private set; }
	public Guid? ReadinessConfirmedByUserId { get; private set; }
	public bool ReadinessOverridden { get; private set; }
	public string? ReadinessReason { get; private set; }
	public int ReadinessApprovalCount { get; private set; }
	public Guid? FirstReadinessApprovedByUserId { get; private set; }
	public DateTime? FirstReadinessApprovedAt { get; private set; }

	protected AdmissionCrmDocumentChecklistAssignment() { }

	public AdmissionCrmDocumentChecklistAssignment(
		Guid id, Guid? tenantId, Guid academicYearId, Guid leadId, Guid registrationApplicationId,
		Guid checklistTemplateId, int checklistTemplateVersion, string templateSnapshotJson, DateTime assignedAt) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		LeadId = leadId;
		RegistrationApplicationId = registrationApplicationId;
		ChecklistTemplateId = checklistTemplateId;
		ChecklistTemplateVersion = checklistTemplateVersion;
		TemplateSnapshotJson = Check.Length(templateSnapshotJson, nameof(templateSnapshotJson), AdmissionCrmConsts.MaxDocumentChecklistJsonLength);
		AssignedAt = assignedAt;
	}

	public bool ConfirmReadiness(Guid userId, int requiredApprovalCount, bool overridden, string? reason, DateTime at)
	{
		if (requiredApprovalCount is < 1 or > 2)
			throw new BusinessException("AdmissionCrm:InvalidReadinessApprovalCount");
		if (ReadinessConfirmedAt.HasValue)
			throw new BusinessException("AdmissionCrm:ReadinessAlreadyConfirmed");
		if (overridden && string.IsNullOrWhiteSpace(reason))
			throw new BusinessException("AdmissionCrm:ReadinessOverrideReasonRequired");
		if (ReadinessApprovalCount == 0)
		{
			FirstReadinessApprovedByUserId = userId;
			FirstReadinessApprovedAt = at;
			ReadinessApprovalCount = 1;
			ReadinessOverridden = overridden;
			ReadinessReason = string.IsNullOrWhiteSpace(reason) ? null : Check.Length(reason.Trim(), nameof(reason), AdmissionCrmConsts.MaxNotesLength);
			if (requiredApprovalCount == 2)
				return false;
		}
		else
		{
			if (FirstReadinessApprovedByUserId == userId)
				throw new BusinessException("AdmissionCrm:DistinctSecondReadinessApproverRequired");
			ReadinessApprovalCount = 2;
			ReadinessOverridden = ReadinessOverridden || overridden;
			if (!string.IsNullOrWhiteSpace(reason))
				ReadinessReason = Check.Length(reason.Trim(), nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		}
		ReadinessConfirmedAt = at;
		ReadinessConfirmedByUserId = userId;
		return true;
	}
}
