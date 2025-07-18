using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students;

public class StudentAdditionalDetail : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string? FormNo { get; private set; }
	public DateTimeOffset? DateOfBirthday { get; private set; }
	public GenderEnum? Gender { get; private set; }
	public string? ProfilePictureUrl { get; private set; }

	public DateTimeOffset? DateOfInquiry { get; private set; }
	public string InquiryNo { get; private set; }
	public Guid? InquiryPreferenceId { get; private set; }
	public string? RegistrationFormNo { get; private set; }
	public double? RegistrationAmount { get; private set; }

	public string? Category { get; private set; }
	public bool? IsBPLStudent { get; private set; }
	public string? BPLCardNo { get; private set; }
	public bool? HaveDisability { get; private set; }
	public string? SecondLanguage { get; private set; }
	public string? EmergencyContactNo { get; private set; }
	public string? EmergencyRelationName { get; private set; }
	public string? EmergencyRelationType { get; private set; }
	public bool? IsSingleParentChild { get; private set; }
	public bool? IsSingleChild { get; private set; }

	private StudentAdditionalDetail()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	public StudentAdditionalDetail(
		Guid id,
		Guid? tenantId,
		DateTimeOffset? dateOfBirthday,
		GenderEnum? gender,
		DateTimeOffset? dateOfInquiry,
		string inquiryNo,
		Guid? inquiryPreferenceId
	)
		: base(id)
	{
		TenantId = tenantId;
		DateOfBirthday = dateOfBirthday;
		Gender = gender;
		DateOfInquiry = dateOfInquiry;
		InquiryNo = inquiryNo;
		InquiryPreferenceId = inquiryPreferenceId;
	}

	public void UpdateDetails(
	DateTimeOffset? dateOfBirth,
	GenderEnum? gender,
	DateTimeOffset? dateOfInquiry,
	Guid? inquiryPreferenceId)
	{
		DateOfBirthday = dateOfBirth;
		Gender = gender;
		DateOfInquiry = dateOfInquiry;
		InquiryPreferenceId = inquiryPreferenceId;
	}

	public void IssueRegistrationForm(
		string formNumber,
		double amount)
	{
		RegistrationFormNo = formNumber;
		RegistrationAmount = amount;
	}
}
