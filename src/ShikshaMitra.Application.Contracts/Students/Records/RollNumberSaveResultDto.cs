using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.Records;

public class RollNumberSaveResultDto
{
	public Guid AcademicTermId { get; set; }
	public int TotalRequested { get; set; }
	public int AppliedCount { get; set; }
	public int SkippedCount { get; set; }
	public int ClearedCount { get; set; }
	public int ConflictCount { get; set; }
	public List<RollNumberSaveItemResultDto> Results { get; set; } = [];
}

public class RollNumberSaveItemResultDto
{
	public Guid StudentId { get; set; }
	public int? OldRollNo { get; set; }
	public int? NewRollNo { get; set; }
	public string Status { get; set; } = string.Empty;
}
