using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;

public class AssignmentPolicySelectionDto
{
	public string Family { get; set; } = "";
	// Null explicitly removes an optional policy; an omitted family inherits the structure.
	public Guid? VersionId { get; set; }
}

public class AssignmentPolicyUpdateDto
{
	public Guid AcademicYearId { get; set; }
	public string ConcurrencyStamp { get; set; } = "";
	public string Reason { get; set; } = "";
	public List<AssignmentPolicySelectionDto> Policies { get; set; } = [];
}
