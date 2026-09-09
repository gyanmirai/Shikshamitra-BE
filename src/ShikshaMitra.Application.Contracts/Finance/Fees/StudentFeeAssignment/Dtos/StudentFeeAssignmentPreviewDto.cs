using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;

public class StudentFeeAssignmentPreviewDto
{
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
