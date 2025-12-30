using System;

namespace ShikshaMitra.Academic.Curriculum;

public class CoreElectiveSubjectDetailDto
{
	public Guid Id { get; set; }
	public required string Name { get; set; }
	public string? Color { get; set; }
}