using System;

namespace ShikshaMitra.Academic.Curriculum;

public class CreateUpdateSubjectDto
{
	public Guid EducationMediumId { get; set; }
	public string Name { get; set; }
	public string Color { get; set; }
}
