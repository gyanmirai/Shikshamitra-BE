using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;

public class StudentFeeStructureResponseDto
{
	public string ConcurrencyStamp { get; set; } = "";
    public StudentFeeAssignmentPreviewDto? Assignment { get; set; }
    public List<string> Warnings { get; set; } = [];

	public Guid StudentFeeStructureId { get; set; }

	public Guid? StandardFeeStructureId { get; set; }

	public Guid StudentId { get; set; }

	public Guid StandardId { get; set; }

	public string StructureName { get; set; } = null!; // The response mapper populates these members after construction.
	public Guid AcademicYearId { get; set; }
	public Guid? ConcessionPolicyId { get; set; }
	public Guid? ConcessionPolicyVersionId { get; set; }
	public string ConcessionPolicyName { get; set; } = string.Empty;
	public string ConcessionPolicyCode { get; set; } = string.Empty;
	public int ConcessionPolicyVersion { get; set; }
	public string ConcessionValueSource { get; set; } = string.Empty;
	public string ConcessionCalculationMethod { get; set; } = string.Empty;
	public decimal? ConcessionPercentageValue { get; set; }
	public decimal? ConcessionFixedAmount { get; set; }
	public decimal? MaximumConcessionPercentage { get; set; }
	public decimal? MaximumConcessionFixedAmount { get; set; }
	public string ConcessionFeeHeadScope { get; set; } = string.Empty;
	public List<Guid> ConcessionApplicableFeeHeadIds { get; set; } = [];
	public string ConcessionApplyOn { get; set; } = string.Empty;
	public Guid? RefundPolicyId { get; set; }
	public Guid? RefundPolicyVersionId { get; set; }
	public Guid? ProrationPolicyId { get; set; }
	public Guid? ProrationPolicyVersionId { get; set; }
	public string? ProrationPolicyName { get; set; }
	public string? ProrationPolicyCode { get; set; }
	public int? ProrationPolicyVersion { get; set; }
	public DateTime? ChargeStartDate { get; set; }
	public DateTime? ChargeEndDate { get; set; }
	public string? ChargePeriodSource { get; set; }

	public List<StudentFeeHeadDto> FeeHeads { get; set; } = new List<StudentFeeHeadDto>();
}
