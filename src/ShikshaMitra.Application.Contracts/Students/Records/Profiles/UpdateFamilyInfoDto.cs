using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.Records.Profiles;

/// <summary>
/// DTO for updating family information of a student in Registration status
/// </summary>
public class UpdateFamilyInfoDto
{
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
	/// List of sibling student IDs studying in the same school
	/// </summary>
	public List<Guid>? SiblingStudentIds { get; set; }
}
