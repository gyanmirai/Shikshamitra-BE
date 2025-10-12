using System;

namespace ShikshaMitra.Academic.Curriculum.Setting;

public class CreateSubjectDto
{
	public required string Name { get; set; }
	public required string Color { get; set; }
	public Guid EducationMediumId { get; set; }
}

