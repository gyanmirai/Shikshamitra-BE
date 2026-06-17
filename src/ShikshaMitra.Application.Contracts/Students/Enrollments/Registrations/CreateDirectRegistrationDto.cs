using System;

namespace ShikshaMitra.Students.Enrollments.Registrations;

/// <summary>
/// DTO for creating a student directly in Registration status.
/// Extends general details with required educational context.
/// </summary>
public class CreateDirectRegistrationDto : RegistrationGeneralInfoBaseDto
{
	/// <summary>
	/// The education medium identifier.
	/// </summary>
	public required Guid AcademicMediumId { get; set; }

	/// <summary>
	/// Required standard/class identifier for direct registration.
	/// </summary>
	public required Guid StandardId { get; set; }

	/// <summary>
	/// Optional division identifier if known at registration time.
	/// </summary>
	public Guid? DivisionId { get; set; }
}
