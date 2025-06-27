using System;

namespace ShikshaMitra.Academic.Curriculum;

public class CreateUpdateSubjectDto
{
	public required string Name { get; set; }
	public required string Color { get; set; }
	public required Guid EducationMediumId { get; set; }
}
