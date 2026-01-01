using System;

namespace ShikshaMitra.Students.Records;

public class RollNumberProjectionDto
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int? RollNo { get; set; }
	public int? GrNo { get; set; }
}
