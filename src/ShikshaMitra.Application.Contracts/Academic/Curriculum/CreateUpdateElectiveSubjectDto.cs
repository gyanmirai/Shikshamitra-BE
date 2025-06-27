using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// DTO for creating or updating an ElectiveSubject.
/// </summary>
public class CreateUpdateElectiveSubjectDto
{
	public required string Name { get; set; }
	public required string Color { get; set; }
	public required Guid EducationMediumId { get; set; }
	public List<Guid> SubjectIds { get; set; } = new List<Guid>();
}
