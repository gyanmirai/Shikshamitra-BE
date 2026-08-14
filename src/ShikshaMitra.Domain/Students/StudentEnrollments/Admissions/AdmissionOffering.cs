using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionOffering : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AdmissionCycleId { get; private set; }
	public Guid? CampusId { get; private set; }
	public Guid BoardId { get; private set; }
	public Guid MediumId { get; private set; }
	public Guid StageId { get; private set; }
	public Guid StandardId { get; private set; }
	public string Code { get; private set; } = string.Empty;
	public DateTime OpenDate { get; private set; }
	public DateTime CloseDate { get; private set; }
	public bool IsActive { get; private set; }
	public Guid? WorkflowDefinitionId { get; private set; }
	public Guid? InquiryFormDefinitionId { get; private set; }
	public Guid? ApplicationFormDefinitionId { get; private set; }

	private AdmissionOffering() { }

	public AdmissionOffering(
		Guid id,
		Guid? tenantId,
		Guid admissionCycleId,
		Guid? campusId,
		Guid boardId,
		Guid mediumId,
		Guid stageId,
		Guid standardId,
		string code,
		DateTime openDate,
		DateTime closeDate)
		: base(id)
	{
		TenantId = tenantId;
		AdmissionCycleId = Check.NotDefaultOrNull<Guid>(admissionCycleId, nameof(admissionCycleId));
		UpdateDetails(campusId, boardId, mediumId, stageId, standardId, code, openDate, closeDate);
		IsActive = true;
	}

	public void UpdateDetails(
		Guid? campusId,
		Guid boardId,
		Guid mediumId,
		Guid stageId,
		Guid standardId,
		string code,
		DateTime openDate,
		DateTime closeDate)
	{
		if (openDate.Date > closeDate.Date)
		{
			throw new BusinessException(AdmissionErrorCodes.OfferingDateRangeInvalid);
		}

		CampusId = campusId;
		BoardId = Check.NotDefaultOrNull<Guid>(boardId, nameof(boardId));
		MediumId = Check.NotDefaultOrNull<Guid>(mediumId, nameof(mediumId));
		StageId = Check.NotDefaultOrNull<Guid>(stageId, nameof(stageId));
		StandardId = Check.NotDefaultOrNull<Guid>(standardId, nameof(standardId));
		Code = Check.NotNullOrWhiteSpace(code, nameof(code), AdmissionOfferingConsts.MaxCodeLength).Trim().ToUpperInvariant();
		OpenDate = openDate.Date;
		CloseDate = closeDate.Date;
	}

	public void ConfigureReferences(Guid? workflowDefinitionId, Guid? inquiryFormDefinitionId, Guid? applicationFormDefinitionId)
	{
		WorkflowDefinitionId = workflowDefinitionId;
		InquiryFormDefinitionId = inquiryFormDefinitionId;
		ApplicationFormDefinitionId = applicationFormDefinitionId;
	}

	public void SetActive(bool isActive) => IsActive = isActive;
}
