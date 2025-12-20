using System;

namespace ShikshaMitra.Students.Enrollments.Registrations;

/// <summary>
/// DTO for creating a student directly in Registration status.
/// Extends UpdateGeneralInfoDto with required educational context.
/// </summary>
public class CreateDirectRegistrationDto : UpdateGeneralInfoDto
{
	/// <summary>
	/// The education medium/stage identifier (e.g., CBSE-Primary, State Board-Secondary)
	/// </summary>
	public required Guid AcademicMediumId { get; set; }

	/// <summary>
	/// Optional standard/class identifier if known at registration time
	/// </summary>
	public Guid? StandardId { get; set; }

	/// <summary>
	/// Optional division identifier if known at registration time
	/// </summary>
	public Guid? DivisionId { get; set; }
}
