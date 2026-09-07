using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;

public class StudentFeeStructureResponseDto
{
	public Guid StudentFeeStructureId { get; set; }

	public Guid? StandardFeeStructureId { get; set; }

	public Guid StudentId { get; set; }

	public Guid StandardId { get; set; }

	public string StructureName { get; set; }
	public Guid AcademicYearId { get; set; }
	public Guid ConcessionPolicyId { get; set; }
	public Guid ConcessionPolicyVersionId { get; set; }
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

	public List<StudentFeeHeadDto> FeeHeads { get; set; } = new List<StudentFeeHeadDto>();
}
