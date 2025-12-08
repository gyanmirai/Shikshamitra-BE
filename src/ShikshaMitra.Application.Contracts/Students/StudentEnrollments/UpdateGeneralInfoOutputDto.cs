using System;

namespace ShikshaMitra.Students.StudentEnrollments;

/// <summary>
/// Output DTO returned after successfully updating general information.
/// </summary>
public class UpdateGeneralInfoOutputDto
{
	public Guid StudentId { get; set; }
	public StudentLifeCycleStatusEnum Status { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string? LastName { get; set; }
	public DateTimeOffset? DateOfBirth { get; set; }
	public GenderEnum? Gender { get; set; }
}
