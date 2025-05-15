using System;

namespace ShikshaMitra.Academic.Curriculum;

public class CreateUpdateSubjectDto
{
	public string Name { get; set; }
	public Guid MediumId { get; set; }
	public string Color { get; set; }
}
