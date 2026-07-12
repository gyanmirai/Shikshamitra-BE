using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRegistrationTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public int Version { get; private set; }
	public AdmissionCrmRegistrationTemplateStatus Status { get; private set; }
	public bool IsFallback { get; private set; }
	public string ApplicabilityJson { get; private set; } = "{}";
	public string SectionsJson { get; private set; } = "[]";
	public int LinkExpiryDays { get; private set; }
	public bool AllowPartialSave { get; private set; }
	public bool FeeRequired { get; private set; }
	public decimal FeeAmount { get; private set; }
	public AdmissionInquiryStage? AutoIssueAtStage { get; private set; }
	public string? Notes { get; private set; }
	public DateTime? PublishedAt { get; private set; }
	public DateTime? RetiredAt { get; private set; }

	protected AdmissionCrmRegistrationTemplate()
	{
	}

	public AdmissionCrmRegistrationTemplate(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		string name,
		int version,
		bool isFallback,
		string applicabilityJson,
		string sectionsJson,
		int linkExpiryDays,
		bool allowPartialSave,
		bool feeRequired,
		decimal feeAmount,
		AdmissionInquiryStage? autoIssueAtStage,
		string? notes)
		: base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		Version = version;
		Status = AdmissionCrmRegistrationTemplateStatus.Draft;
		Update(name, isFallback, applicabilityJson, sectionsJson, linkExpiryDays, allowPartialSave, feeRequired, feeAmount, autoIssueAtStage, notes);
	}

	public void Update(
		string name,
		bool isFallback,
		string applicabilityJson,
		string sectionsJson,
		int linkExpiryDays,
		bool allowPartialSave,
		bool feeRequired,
		decimal feeAmount,
		AdmissionInquiryStage? autoIssueAtStage,
		string? notes)
	{
		EnsureDraft();
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), AdmissionCrmConsts.MaxTemplateNameLength);
		ApplicabilityJson = Check.Length(applicabilityJson, nameof(applicabilityJson), AdmissionCrmConsts.MaxRegistrationTemplateJsonLength);
		SectionsJson = Check.Length(sectionsJson, nameof(sectionsJson), AdmissionCrmConsts.MaxRegistrationTemplateJsonLength);
		if (linkExpiryDays is < 1 or > 365)
		{
			throw new BusinessException("AdmissionCrm:InvalidRegistrationLinkExpiryDays");
		}
		if (feeAmount is < 0 or > 999999.99m)
		{
			throw new BusinessException("AdmissionCrm:InvalidRegistrationFeeAmount");
		}
		IsFallback = isFallback;
		LinkExpiryDays = linkExpiryDays;
		AllowPartialSave = allowPartialSave;
		FeeRequired = feeRequired;
		FeeAmount = feeRequired ? feeAmount : 0;
		AutoIssueAtStage = autoIssueAtStage;
		Notes = string.IsNullOrWhiteSpace(notes)
			? null
			: Check.Length(notes.Trim(), nameof(notes), AdmissionCrmConsts.MaxNotesLength);
	}

	public void Publish(DateTime publishedAt)
	{
		EnsureDraft();
		Status = AdmissionCrmRegistrationTemplateStatus.Published;
		PublishedAt = publishedAt;
	}

	public void Retire(DateTime retiredAt)
	{
		if (Status != AdmissionCrmRegistrationTemplateStatus.Published)
		{
			throw new BusinessException("AdmissionCrm:OnlyPublishedRegistrationTemplateCanBeRetired");
		}
		Status = AdmissionCrmRegistrationTemplateStatus.Retired;
		RetiredAt = retiredAt;
	}

	private void EnsureDraft()
	{
		if (Status != AdmissionCrmRegistrationTemplateStatus.Draft)
		{
			throw new BusinessException("AdmissionCrm:PublishedRegistrationTemplateIsImmutable");
		}
	}
}
