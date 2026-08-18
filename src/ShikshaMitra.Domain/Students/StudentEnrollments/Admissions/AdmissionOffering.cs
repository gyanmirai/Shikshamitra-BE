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
	public DateTimeOffset? OpenDate { get; private set; }
	public DateTimeOffset? CloseDate { get; private set; }
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
		bool useCycleDates,
		DateTimeOffset? openDate,
		DateTimeOffset? closeDate)
		: base(id)
	{
		TenantId = tenantId;
		AdmissionCycleId = Check.NotDefaultOrNull<Guid>(admissionCycleId, nameof(admissionCycleId));
		UpdateIdentity(campusId, boardId, mediumId, stageId, standardId, identityLocked: false);
		UpdateOperationalDetails(code, useCycleDates, openDate, closeDate);
		IsActive = true;
	}

	public void UpdateIdentity(
		Guid? campusId,
		Guid boardId,
		Guid mediumId,
		Guid stageId,
		Guid standardId,
		bool identityLocked)
	{
		var identityChanged = CampusId != campusId || BoardId != boardId || MediumId != mediumId ||
			StageId != stageId || StandardId != standardId;
		if (identityLocked && identityChanged)
		{
			throw new BusinessException(AdmissionErrorCodes.OfferingIdentityLocked);
		}

		CampusId = campusId;
		BoardId = Check.NotDefaultOrNull<Guid>(boardId, nameof(boardId));
		MediumId = Check.NotDefaultOrNull<Guid>(mediumId, nameof(mediumId));
		StageId = Check.NotDefaultOrNull<Guid>(stageId, nameof(stageId));
		StandardId = Check.NotDefaultOrNull<Guid>(standardId, nameof(standardId));
	}

	public void UpdateOperationalDetails(
		string code,
		bool useCycleDates,
		DateTimeOffset? openDate,
		DateTimeOffset? closeDate)
	{
		if (!useCycleDates && (!openDate.HasValue || !closeDate.HasValue))
		{
			throw new BusinessException(AdmissionErrorCodes.OfferingDateRangeInvalid);
		}

		if (openDate.HasValue && closeDate.HasValue && openDate.Value.Date > closeDate.Value.Date)
		{
			throw new BusinessException(AdmissionErrorCodes.OfferingDateRangeInvalid);
		}

		Code = Check.NotNullOrWhiteSpace(code, nameof(code), AdmissionOfferingConsts.MaxCodeLength).Trim().ToUpperInvariant();
		OpenDate = useCycleDates ? null : NormalizeDate(openDate!.Value);
		CloseDate = useCycleDates ? null : NormalizeDate(closeDate!.Value);
	}

	public void ConfigureReferences(Guid? workflowDefinitionId, Guid? inquiryFormDefinitionId, Guid? applicationFormDefinitionId)
	{
		WorkflowDefinitionId = workflowDefinitionId;
		InquiryFormDefinitionId = inquiryFormDefinitionId;
		ApplicationFormDefinitionId = applicationFormDefinitionId;
	}

	public DateTimeOffset GetEffectiveOpenDate(DateTime cycleStartDate) =>
		OpenDate ?? ToUtcDateOffset(cycleStartDate);

	public DateTimeOffset GetEffectiveCloseDate(DateTime cycleEndDate) =>
		CloseDate ?? ToUtcDateOffset(cycleEndDate);

	public void Activate() => IsActive = true;

	public void Deactivate() => IsActive = false;

	private static DateTimeOffset NormalizeDate(DateTimeOffset value) =>
		new(value.Year, value.Month, value.Day, 0, 0, 0, value.Offset);

	private static DateTimeOffset ToUtcDateOffset(DateTime value) =>
		new(DateTime.SpecifyKind(value.Date, DateTimeKind.Utc));
}
