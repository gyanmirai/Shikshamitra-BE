using System;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionInquiry : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string InquiryNumber { get; private set; }
	public Guid AdmissionCycleId { get; private set; }
	public Guid? AdmissionOfferingId { get; private set; }
	public Guid? FamilyId { get; private set; }
	public Guid? ApplicantId { get; private set; }
	public Guid? AssignedCounsellorId { get; private set; }
	public string ApplicantName { get; private set; }
	public string NormalizedPhone { get; private set; }
	public string? Email { get; private set; }
	public AdmissionInquirySource Source { get; private set; }
	public string? SourceDetail { get; private set; }
	public AdmissionInquiryStatus Status { get; private set; }
	public Guid? DuplicateOfId { get; private set; }
	public DateTime? ContactedAt { get; private set; }
	public DateTime? LastActivityAt { get; private set; }

	protected AdmissionInquiry() { }

	public AdmissionInquiry(Guid id, Guid? tenantId, Guid admissionCycleId, Guid? admissionOfferingId,
		string applicantName, string phone, string? email, AdmissionInquirySource source, string? sourceDetail)
		: base(id)
	{
		TenantId = tenantId;
		InquiryNumber = $"INQ/{DateTime.UtcNow:yyyy}/{Guid.NewGuid():N}"[..20];
		AdmissionCycleId = Check.NotDefaultOrNull<Guid>(admissionCycleId, nameof(admissionCycleId));
		AdmissionOfferingId = admissionOfferingId;
		ApplicantName = Check.NotNullOrWhiteSpace(applicantName, nameof(applicantName)).Trim();
		NormalizedPhone = NormalizePhone(phone);
		Email = NormalizeNullable(email);
		Source = source;
		SourceDetail = NormalizeNullable(sourceDetail);
		Status = AdmissionInquiryStatus.New;
		LastActivityAt = DateTime.UtcNow;
	}

	public void MarkContacted(DateTime contactedAt)
	{
		Status = AdmissionInquiryStatus.Contacted;
		ContactedAt = contactedAt;
		LastActivityAt = contactedAt;
	}

	public void SetStatus(AdmissionInquiryStatus status, Guid? duplicateOfId = null)
	{
		Status = status;
		DuplicateOfId = duplicateOfId;
		LastActivityAt = DateTime.UtcNow;
	}

	public void LinkIdentity(Guid familyId, Guid applicantId)
	{
		FamilyId = Check.NotDefaultOrNull<Guid>(familyId, nameof(familyId));
		ApplicantId = Check.NotDefaultOrNull<Guid>(applicantId, nameof(applicantId));
	}

	public void AssignCounsellor(Guid counsellorId)
	{
		AssignedCounsellorId = Check.NotDefaultOrNull<Guid>(counsellorId, nameof(counsellorId));
		LastActivityAt = DateTime.UtcNow;
	}

	private static string NormalizePhone(string phone)
	{
		var normalized = Check.NotNullOrWhiteSpace(phone, nameof(phone)).Trim();
		return string.Concat(normalized.Where(char.IsDigit));
	}

	private static string? NormalizeNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
