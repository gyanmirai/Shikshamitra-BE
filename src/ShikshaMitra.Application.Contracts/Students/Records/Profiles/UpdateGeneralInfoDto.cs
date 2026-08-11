using System;

namespace ShikshaMitra.Students.Records.Profiles;

/// <summary>
/// DTO for updating general information of a student in RegistrationFormIssued status.
/// Upon successful validation, this will transition the student to Registration status.
/// </summary>
public class UpdateGeneralInfoDto : RegistrationGeneralInfoBaseDto
{
	public Guid? AcademicMediumId { get; set; }
	public Guid? StandardId { get; set; }
	public Guid? DivisionId { get; set; }
}
