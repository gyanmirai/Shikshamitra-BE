using ShikshaMitra.Students.StudentEnrollments.Registration;
using System;

namespace ShikshaMitra.Students.StudentEnrollments;

/// <summary>
/// DTO for returning additional details information
/// </summary>
public class StudentAdditionalDetailsDto
{
	public Guid StudentId { get; set; }
	public StudentLifeCycleStatusEnum Status { get; set; }

	/// <summary>
	/// Student category (General, RTE, etc.)
	/// </summary>
	public Category Category { get; set; } = Category.General;

	/// <summary>
	/// Indicates if student is a BPL (Below Poverty Line) student
	/// </summary>
	public bool IsBPLStudent { get; set; }

	/// <summary>
	/// BPL Card Number if applicable
	/// </summary>
	public string? BPLCardNo { get; set; }

	/// <summary>
	/// Second language preference
	/// </summary>
	public string? SecondLanguage { get; set; }

	/// <summary>
	/// Indicates if student is a person with disability
	/// </summary>
	public bool HaveDisability { get; set; }

	/// <summary>
	/// Emergency contact person relative name
	/// </summary>
	public string? EmergencyRelativeName { get; set; }

	/// <summary>
	/// Relationship with emergency contact person
	/// </summary>
	public string? EmergencyRelation { get; set; }

	/// <summary>
	/// Emergency contact phone number
	/// </summary>
	public string? EmergencyContactNo { get; set; }

	/// <summary>
	/// Indicates if student is a single child
	/// </summary>
	public bool IsSingleChild { get; set; }

	/// <summary>
	/// Indicates if student is from a single parent family
	/// </summary>
	public bool IsSingleParentChild { get; set; }
}
