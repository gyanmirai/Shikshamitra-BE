using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments;
public class StudentInquiryDto : EntityDto
{
	public Guid StudentId { get; set; }
	public required string InquiryNo { get; set; }
	public required string StudentName { get; set; }
	public required string Standard { get; set; }
	public DateTimeOffset? DateOfBirth { get; set; }
	public string? Age { get; set; }
	public DateTimeOffset DateOfInquiry { get; set; }
	public string? Gender { get; set; }
	public string? Address { get; set; }
	public string? PrimaryContactNo { get; set; }
	public string? SecondaryContactNo { get; set; }
	public string? PreviousSchool { get; set; }
	public string? InquiryPreference { get; set; }
	public DateTimeOffset? NextReminderDate { get; set; }
}
