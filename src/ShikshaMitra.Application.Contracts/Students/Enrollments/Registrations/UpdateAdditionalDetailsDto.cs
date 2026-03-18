using ShikshaMitra.Students.StudentEnrollments.Registration;
using System;

namespace ShikshaMitra.Students.Enrollments.Registrations;

/// <summary>
/// DTO for updating additional details of a student in Registration status
/// </summary>
public class UpdateAdditionalDetailsDto
{
	/// <summary>
	/// Student category (General, RTE, etc.)
	/// </summary>
	public Category Category { get; set; }

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

	/// <summary>
	/// Indicates if student is library member or not
	/// </summary>
	public bool IsLibraryMember { get; set; } = false;

	/// <summary>
	/// Indicates if student is transport member or not
	/// </summary>
	public bool IsTransportMember { get; set; } = false;
}
