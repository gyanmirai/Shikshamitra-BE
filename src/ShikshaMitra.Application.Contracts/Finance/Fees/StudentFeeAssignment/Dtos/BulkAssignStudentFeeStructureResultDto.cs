using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;

public class BulkAssignStudentFeeStructureResultDto
{
	public int SuccessCount { get; set; }
	public int SkippedCount { get; set; }
	public List<BulkAssignStudentFeeStructureResultItemDto> Results { get; set; } = new();
}

public class BulkAssignStudentFeeStructureResultItemDto
{
	public Guid StudentId { get; set; }
	public bool Success { get; set; }
	public string? ErrorMessage { get; set; }
}
