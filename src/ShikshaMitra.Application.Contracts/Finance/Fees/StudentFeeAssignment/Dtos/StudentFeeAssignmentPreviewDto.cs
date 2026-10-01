using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;

public class StudentFeeAssignmentPreviewDto
{
	public string? PolicyOverrideReason { get; set; }
	public List<AssignmentPolicyDto> OriginalPolicies { get; set; } = [];
	public string? OverrideReason { get; set; }
    public Guid StudentId { get; set; }
    public Guid AcademicYearId { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid StructureId { get; set; }
    public string StructureName { get; set; } = "";
    public int RevisionNumber { get; set; }
    public string Currency { get; set; } = "INR";
    public string CalculationVersion { get; set; } = "assignment-v2";
    public string PreviewToken { get; set; } = "";
    public List<AssignmentHeadDto> Heads { get; set; } = [];
    public List<AssignmentPolicyDto> Policies { get; set; } = [];
    public List<string> Warnings { get; set; } = [];

	public Guid? ProrationPolicyId { get; set; }
	public Guid? ProrationPolicyVersionId { get; set; }
	public DateTime ChargeStartDate { get; set; }
	public DateTime ChargeEndDate { get; set; }
	public decimal ScheduledTotal { get; set; }
	public decimal ProratedTotal { get; set; }
	public decimal ReductionTotal { get; set; }
	public List<StudentFeeAssignmentPreviewLineDto> Lines { get; set; } = [];
}

public class StudentFeeAssignmentPreviewLineDto
{
    public Guid? DueFeeId { get; set; }
    public int Sequence { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? DemandDate { get; set; }
    public decimal AllocationPercentage { get; set; }
    public Guid? PaymentTermVersionId { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }

	public Guid FeeHeadId { get; set; }
	public string FeeHeadName { get; set; } = string.Empty;
	public string InstallmentLabel { get; set; } = string.Empty;
	public DateTime? CoverageStartDate { get; set; }
	public DateTime? CoverageEndDate { get; set; }
	public decimal ScheduledAmount { get; set; }
	public decimal ProratedAmount { get; set; }
	public decimal ReductionAmount { get; set; }
	public decimal? Factor { get; set; }
	public string? RuleType { get; set; }
	public string? RoundingMode { get; set; }
	public string? Explanation { get; set; }
}
