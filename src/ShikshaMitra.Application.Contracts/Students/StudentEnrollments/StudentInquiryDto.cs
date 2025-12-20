using ShikshaMitra.Common;
using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments;
public class StudentInquiryDto : EntityDto
{
	public Guid StudentId { get; set; }
	public required string InquiryNo { get; set; }
	public required string StudentName { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string? LastName { get; set; }
	public required Guid StandardId { get; set; }
	public required string Standard { get; set; }
	public Guid AcademicMediumId { get; set; }
	public string? AcademicMediumName { get; set; }
	public Guid? AcademicBoardId { get; set; }
	public string? AcademicBoardName { get; set; }
	public Guid? AcademicYearId { get; set; }
	public string? AcademicYearName { get; set; }
	public DateTimeOffset? DateOfBirth { get; set; }
	public string? Age { get; set; }
	public DateTimeOffset? DateOfInquiry { get; set; }
	public string? Gender { get; set; }
	public string? Address { get; set; }
	public string? Address1 { get; private set; }
	public string? Address2 { get; private set; }
	public string? Address3 { get; private set; }
	public string? City { get; private set; }
	public Country? Country { get; set; }
	public State? State { get; set; }
	public string? PinCode { get; set; }
	public string? PrimaryContactNo { get; set; }
	public string? SecondaryContactNo { get; set; }
	public string? Email { get; set; }
	public string? PreviousSchool { get; set; }
	public Guid? InquiryPreferenceId { get; set; }
	public string? InquiryPreference { get; set; }
	public DateTimeOffset? NextReminderDate { get; set; }
}
