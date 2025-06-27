using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// DTO for creating or updating an ElectiveSubject.
/// </summary>
public class CreateUpdateElectiveSubjectDto
{
	public Guid EducationMediumId { get; set; }

	public required string Name { get; set; }

	public required string Color { get; set; }

	/// <summary>
	/// IDs of the subjects to be included in this elective subject.
	/// </summary>
	public List<Guid> SubjectIds { get; set; } = new List<Guid>();
}
