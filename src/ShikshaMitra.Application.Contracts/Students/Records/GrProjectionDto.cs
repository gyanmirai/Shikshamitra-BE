using System;

namespace ShikshaMitra.Students.Records;

public class GrProjectionDto
{
	public Guid StudentId { get; set; }
	public int? GrNumber { get; set; }
	public string? CurrentGrNumber { get; set; }
	public string StudentName { get; set; } = string.Empty;
}
