using System;

namespace ShikshaMitra.Students.Enrollments.Registrations;

/// <summary>
/// DTO for updating previous school details of a student in Registration status
/// </summary>
public class UpdatePreviousSchoolDetailsDto
{
	/// <summary>
	/// Name of the previous school
	/// </summary>
	public string? SchoolName { get; set; }

	/// <summary>
	/// FE registration summary field stored on student additional details
	/// </summary>
	public string? PreviousSchool { get; set; }

	/// <summary>
	/// School address
	/// </summary>
	public string? SchoolAddress { get; set; }

	/// <summary>
	/// Education board of previous school
	/// </summary>
	public string? Board { get; set; }

	/// <summary>
	/// Medium of instruction at previous school
	/// </summary>
	public string? Medium { get; set; }

	/// <summary>
	/// Last class/standard attended
	/// </summary>
	public string? LastClassAttended { get; set; }

	/// <summary>
	/// FE registration summary field stored on student additional details
	/// </summary>
	public string? LastClassPassed { get; set; }

	/// <summary>
	/// Previous school's index number
	/// </summary>
	public string? SchoolIndexNumber { get; set; }

	/// <summary>
	/// Transfer Certificate (TC) number
	/// </summary>
	public string? TCNumber { get; set; }

	/// <summary>
	/// Date of leaving previous school
	/// </summary>
	public DateTimeOffset? DateOfLeaving { get; set; }

	/// <summary>
	/// Percentage/marks obtained in last class
	/// </summary>
	public decimal? PercentageObtained { get; set; }

	/// <summary>
	/// Academic year of last attendance
	/// </summary>
	public string? AcademicYear { get; set; }

	/// <summary>
	/// Reason for leaving previous school
	/// </summary>
	public string? ReasonForLeaving { get; set; }

	/// <summary>
	/// Any additional remarks
	/// </summary>
	public string? Remarks { get; set; }
}
