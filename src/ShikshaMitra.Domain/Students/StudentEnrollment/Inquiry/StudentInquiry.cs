using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentEnrollment.Settings;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.studentenrollment.Inquiry;

public class StudentInquiry : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string InquiryNumber { get; private set; }
	public string FirstName { get; private set; }
	public string MiddleName { get; private set; }
	public string LastName { get; private set; }
	public string FormNumber { get; private set; }
	public string PreviousSchoolName { get; private set; }
	public DateOnly DateOfBirth { get; private set; }
	public string Age { get; private set; }
	public DateOnly DateOfInquiry { get; private set; }
	public GenderType GenderType { get; private set; }
	public string ParentContactNumber { get; private set; }
	public string AlternativeContactNumber { get; private set; }
	public string City { get; private set; }
	public string State { get; private set; }
	public int PinCode { get; private set; }
	public string Address { get; private set; }
	// public string InquiryPreferences { get; private set; }
	
	// Foreign Key 
	public Guid EducationMediumId { get; private set; }
	public Guid EducationBoardId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid StanderdId { get; private set; }
	public Guid InquiryPreferenceId { get; private set; }
	
	// Navigation Property for Role 
	public EducationMedium EducationMedium { get; private set; }
	
	public EducationBoard EducationBoard { get; private set; }
	public AcademicYear AcademicYear { get; private set; }
	public Standard Standard { get; private set; }	
	
	public InquiryPreference InquiryPreference { get; private set; }
	
	private StudentInquiry() {} // For ORM
	
	internal StudentInquiry(
		Guid id,
		Guid? tenantId,
		string inquiryNumber,
		string firstName,
		string middleName,
		string lastName,
		string formNumber,
		string previousSchoolName,
		DateOnly dateOfBirth,
		string age,
		DateOnly dateOfInquiry,
		GenderType genderType,
		string parentContactNumber,
		string alternativeContactNumber,
		string city,
		string state,
		int pinCode,
		string address,
		Guid educationMediumId,
		Guid educationBoardId,
		Guid academicYearId,
		Guid standerdId,
		Guid inquiryPreferenceId
		) :  base(id)
	{
		TenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
		InquiryNumber = inquiryNumber;
		FirstName = firstName;
		MiddleName = middleName;
		LastName = lastName;
		FormNumber = formNumber;
		PreviousSchoolName = previousSchoolName;
		DateOfBirth = dateOfBirth;
		Age = age;
		DateOfInquiry = dateOfInquiry;
		GenderType = genderType;
		ParentContactNumber = parentContactNumber;
		AlternativeContactNumber = alternativeContactNumber;
		City = city;
		State = state;
		PinCode = pinCode;
		Address = address;
		EducationMediumId = educationMediumId;
		EducationBoardId = educationBoardId;
		AcademicYearId = academicYearId;
		StanderdId = standerdId;
		InquiryPreferenceId = inquiryPreferenceId;
	}
}

