using System;

namespace ShikshaMitra.Students.Admissions;

/// <summary>
/// DTO for student admission list item
/// </summary>
public class StudentAdmissionListItemDto
{
	public Guid StudentId { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string? LastName { get; set; }
	public string FullName => $"{FirstName} {MiddleName} {LastName}".Trim();
	public string? RegistrationNo { get; set; }
	public Guid? DivisionId { get; set; }
	public string? DivisionName { get; set; }
	public Guid? StandardId { get; set; }
	public string? StandardName { get; set; }
	public string ApprovalStatus { get; set; } = null!;
	public string? ApprovalRemarks { get; set; }
	public int? RollNo { get; set; }
	public int? GrNo { get; set; }
}
