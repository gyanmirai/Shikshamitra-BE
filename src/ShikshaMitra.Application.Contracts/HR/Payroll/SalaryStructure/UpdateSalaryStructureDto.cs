using System.Collections.Generic;

namespace ShikshaMitra.HR.Payroll.SalaryStructure;

/// <summary>
/// DTO for updating employee salary structure.
/// </summary>
public class UpdateSalaryStructureDto
{
	/// <summary>
	/// List of earning components.
	/// </summary>
	public List<SalaryComponentDto> EarningComponents { get; set; } = new();

	/// <summary>
	/// List of deduction components.
	/// </summary>
	public List<SalaryComponentDto> DeductionComponents { get; set; } = new();
}

