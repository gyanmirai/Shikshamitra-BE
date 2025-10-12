using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// DTO for creating or updating an ElectiveGroup.
/// </summary>
public class CreateElectiveGroupDto
{
	public required string Name { get; set; }
	public required string Color { get; set; }
	public required Guid EducationMediumId { get; set; }
	public List<Guid> SubjectIds { get; set; } = new List<Guid>();
}