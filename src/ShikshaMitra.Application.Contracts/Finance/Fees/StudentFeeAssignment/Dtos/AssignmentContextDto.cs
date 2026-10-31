using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;

/// <summary>Read-only resolution of the student/year's assignment and admission source facts.</summary>
public class AssignmentContextDto
{
	public Guid StudentId { get; set; }
	public Guid AcademicYearId { get; set; }
	public string Status { get; set; } = "Unassigned";
	public string Source { get; set; } = "Finance";
	public Guid? AssignmentId { get; set; }
	public Guid? ApplicationId { get; set; }
	public Guid? FinanceFeeAccountId { get; set; }
	public Guid? StructureId { get; set; }
	public int? StructureRevision { get; set; }
	public DateTime? JoiningDate { get; set; }
	public DateTime? ChargeStartDate { get; set; }
	public DateTime? ChargeEndDate { get; set; }
	public AssignmentPolicyDto? ProrationPolicy { get; set; }
	public List<Guid> AdmissionFeeHeadIds { get; set; } = [];
	public List<Guid> SelectedOptionalFeeHeadIds { get; set; } = [];
	public List<VariableFeeHeadAmountDto> VariableFeeHeads { get; set; } = [];
	public string? Blocker { get; set; }
	public string? ErrorCode { get; set; }
	public StudentFeeAssignmentPreviewDto? PreparedAssignment { get; set; }
}

public class AssignmentContextsInput
{
	public Guid AcademicYearId { get; set; }
	public List<Guid> StudentIds { get; set; } = [];
}
