using System.Collections.Generic;

namespace ShikshaMitra.HR.Payroll.SalaryStructure;

/// <summary>
/// Response DTO for employee salary structure.
/// </summary>
public class SalaryStructureResponseDto
{
	/// <summary>
	/// List of earning components.
	/// </summary>
	public List<SalaryComponentDto> EarningComponents { get; set; } = new();

	/// <summary>
	/// List of deduction components.
	/// </summary>
	public List<SalaryComponentDto> DeductionComponents { get; set; } = new();
	public System.Guid? PfRuleId { get; set; }
	public string? QuotedSalaryBasis { get; set; }
	public decimal? QuotedMonthlyAmount { get; set; }
	public System.Guid? SourceSalaryTemplateId { get; set; }
	public System.Guid? SourceSalaryTemplateVersionId { get; set; }
	public int? SourceSalaryTemplateVersion { get; set; }
	public System.DateTime? SalaryGeneratedAt { get; set; }
	public bool WasModifiedAfterGeneration { get; set; }
}

