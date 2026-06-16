using System;

namespace ShikshaMitra.Students.Enrollments.Registrations;

public class StudentSearchResultDto
{
	public Guid Id { get; set; }
	public string Student { get; set; } = string.Empty;
	public string StudentName { get; set; } = string.Empty;
	public string? FormNo { get; set; }
	public int? GrNo { get; set; }
	public string? AcademicBoard { get; set; }
	public string? EducationMedium { get; set; }
	public string? Standard { get; set; }
	public string? Division { get; set; }
	public StudentLifeCycleStatusEnum Status { get; set; }
}
