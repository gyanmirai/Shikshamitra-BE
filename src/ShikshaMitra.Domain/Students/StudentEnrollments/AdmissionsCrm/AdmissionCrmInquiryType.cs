using System;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmInquiryType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string Code { get; private set; } = string.Empty;
	public string DisplayName { get; private set; } = string.Empty;
	public string? Description { get; private set; }
	public int DisplayOrder { get; private set; }
	public bool IsActive { get; private set; }
	public string? DeactivationReason { get; private set; }

	protected AdmissionCrmInquiryType()
	{
	}

	public AdmissionCrmInquiryType(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		string code,
		string displayName,
		string? description,
		int displayOrder)
		: base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		Update(code, displayName, description, displayOrder);
		IsActive = true;
	}

	public void Update(string code, string displayName, string? description, int displayOrder)
	{
		Code = NormalizeCode(code);
		DisplayName = Check.NotNullOrWhiteSpace(
			displayName,
			nameof(displayName),
			AdmissionCrmConsts.MaxSourceSnapshotLength);
		Description = NormalizeOptional(description, AdmissionCrmConsts.MaxDescriptionLength);
		if (displayOrder < 0)
		{
			throw new BusinessException("AdmissionCrm:InvalidInquiryTypeDisplayOrder");
		}

		DisplayOrder = displayOrder;
	}

	public void SetActive(bool active, string? reason)
	{
		if (!active && string.IsNullOrWhiteSpace(reason))
		{
			throw new BusinessException("AdmissionCrm:InquiryTypeDeactivationReasonRequired");
		}

		IsActive = active;
		DeactivationReason = active
			? null
			: NormalizeOptional(reason, AdmissionCrmConsts.MaxNotesLength);
	}

	private static string NormalizeCode(string value)
	{
		var code = Check.NotNullOrWhiteSpace(
				value,
				nameof(value),
				AdmissionCrmConsts.MaxPlaybookCodeLength)
			.Trim()
			.ToUpperInvariant();
		if (code.Any(character =>
				!char.IsLetterOrDigit(character) &&
				character is not '-' and not '_'))
		{
			throw new BusinessException("AdmissionCrm:InvalidInquiryTypeCode");
		}

		return code;
	}

	private static string? NormalizeOptional(string? value, int maxLength) =>
		string.IsNullOrWhiteSpace(value)
			? null
			: Check.Length(value.Trim(), nameof(value), maxLength);
}
