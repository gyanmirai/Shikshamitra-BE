using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.Records.Profiles;

/// <summary>
/// DTO for returning family information
/// </summary>
public class StudentFamilyInfoDto
{
	public Guid StudentId { get; set; }
	public StudentLifeCycleStatusEnum Status { get; set; }

	/// <summary>
	/// Father details
	/// </summary>
	public ParentGuardianDto? FatherDetails { get; set; }

	/// <summary>
	/// Mother details
	/// </summary>
	public ParentGuardianDto? MotherDetails { get; set; }

	/// <summary>
	/// Guardian details (if different from parents)
	/// </summary>
	public ParentGuardianDto? GuardianDetails { get; set; }

	/// <summary>
	/// List of sibling student information
	/// </summary>
	public List<SiblingStudentDto>? SiblingStudents { get; set; }
}

/// <summary>
/// DTO for sibling student basic information
/// </summary>
public class SiblingStudentDto
{
	public Guid StudentId { get; set; }
	public string? FullName { get; set; }
	public string? StandardName { get; set; }
	public string? DivisionName { get; set; }
	public int? GrNo { get; set; }
}
