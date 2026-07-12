using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmDocumentChecklistTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public int Version { get; private set; }
	public AdmissionCrmDocumentChecklistTemplateStatus Status { get; private set; }
	public bool IsFallback { get; private set; }
	public string ApplicabilityJson { get; private set; } = "{}";
	public string RequirementsJson { get; private set; } = "[]";
	public int ReadinessApprovalCount { get; private set; }
	public string? Notes { get; private set; }
	public DateTime? PublishedAt { get; private set; }
	public DateTime? RetiredAt { get; private set; }

	protected AdmissionCrmDocumentChecklistTemplate() { }

	public AdmissionCrmDocumentChecklistTemplate(
		Guid id, Guid? tenantId, Guid academicYearId, string name, int version, bool isFallback,
		string applicabilityJson, string requirementsJson, int readinessApprovalCount, string? notes) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		Version = version;
		Status = AdmissionCrmDocumentChecklistTemplateStatus.Draft;
		Update(name, isFallback, applicabilityJson, requirementsJson, readinessApprovalCount, notes);
	}

	public void Update(string name, bool isFallback, string applicabilityJson, string requirementsJson, int readinessApprovalCount, string? notes)
	{
		EnsureDraft();
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), AdmissionCrmConsts.MaxTemplateNameLength);
		ApplicabilityJson = Check.Length(applicabilityJson, nameof(applicabilityJson), AdmissionCrmConsts.MaxDocumentChecklistJsonLength);
		RequirementsJson = Check.Length(requirementsJson, nameof(requirementsJson), AdmissionCrmConsts.MaxDocumentChecklistJsonLength);
		if (readinessApprovalCount is < 1 or > 2)
			throw new BusinessException("AdmissionCrm:InvalidReadinessApprovalCount");
		ReadinessApprovalCount = readinessApprovalCount;
		IsFallback = isFallback;
		Notes = string.IsNullOrWhiteSpace(notes) ? null : Check.Length(notes.Trim(), nameof(notes), AdmissionCrmConsts.MaxNotesLength);
	}

	public void Publish(DateTime at)
	{
		EnsureDraft();
		Status = AdmissionCrmDocumentChecklistTemplateStatus.Published;
		PublishedAt = at;
	}

	public void Retire(DateTime at)
	{
		if (Status != AdmissionCrmDocumentChecklistTemplateStatus.Published)
			throw new BusinessException("AdmissionCrm:OnlyPublishedDocumentChecklistCanBeRetired");
		Status = AdmissionCrmDocumentChecklistTemplateStatus.Retired;
		RetiredAt = at;
	}

	private void EnsureDraft()
	{
		if (Status != AdmissionCrmDocumentChecklistTemplateStatus.Draft)
			throw new BusinessException("AdmissionCrm:PublishedDocumentChecklistIsImmutable");
	}
}
