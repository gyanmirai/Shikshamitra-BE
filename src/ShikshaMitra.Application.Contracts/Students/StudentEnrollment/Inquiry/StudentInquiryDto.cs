using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentEnrollment.Settings;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.Students.StudentEnrollment.Inquiry;

public class StudentInquiryDto : AuditedEntityDto<Guid>
{
	public string InquiryNumber { get; set; }
	public string FirstName { get; set; }
	public string MiddleName { get; set; }
	public string LastName { get; set; }
	public string FormNumber { get; set; }
	public string PreviousSchoolName { get; set; }
	public DateTimeOffset DateOfBirth { get; set; }
	public int Age { get; set; }
	public DateTimeOffset DateOfInquiry { get; set; }
	public GenderType GenderType { get; set; }
	public string ParentContactNumber { get; set; }
	public string AlternativeContactNumber { get; set; }
	public string City { get; set; }
	public string State { get; set; }
	public int PinCode { get; set; }
	public string Address { get; set; }
	
	// value Object
	public Guid EducationMediumId { get; set; }
	public Guid AcademicYearId { get; set; }
	public Guid StanderdId { get; set; }
	public Guid InquiryPreferenceId { get; set; }
	public EducationMediumDto EducationMedium { get; set; }
	public AcademicYearDto AcademicYear { get; set; }
	public StandardDto Standard { get; set; }
	public InquiryPreferenceDto InquiryPreference { get; set; }
}