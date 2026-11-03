using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.Enrollments.Registrations;

/// <summary>
/// DTO for updating family information of a student in Registration status
/// </summary>
public class UpdateFamilyInfoDto
{
	[RegularExpression("^(father|mother|guardian)$")]
	public string? MainContactRelationship { get; set; }
	/// <summary>Staff acknowledgment required when changing the main school contact to Guardian.</summary>
	public bool? GuardianResponsibilityConfirmed { get; set; }
	[StringLength(128)]
	public string? GuardianRelationship { get; set; }
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
