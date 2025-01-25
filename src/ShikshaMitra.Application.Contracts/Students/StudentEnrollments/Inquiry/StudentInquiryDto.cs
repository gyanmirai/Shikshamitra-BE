using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using ShikshaMitra.TenantSettings.AcademicSetups.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;

using System;
using Volo.Abp.Application.Dtos;

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
	public Guid EducationBoardId { get; set; }
	public Guid AcademicYearId { get; set; }
	public Guid StandardId { get; set; }
	public Guid InquiryPreferenceId { get; set; }
	public EducationMediumDto EducationMedium { get; set; }
	public EducationBoardDto EducationBoard { get; set; }
	public AcademicYearDto AcademicYear { get; set; }
	public StandardDto Standard { get; set; }
	public InquiryPreferenceDto InquiryPreference { get; set; }
}