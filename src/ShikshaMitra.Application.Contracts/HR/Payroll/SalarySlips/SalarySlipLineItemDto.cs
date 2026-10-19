namespace ShikshaMitra.HR.Payroll.SalarySlips;

/// <summary>
/// DTO for salary slip line item (earning or deduction).
/// </summary>
public class SalarySlipLineItemDto
{
	public System.Guid? ComponentId { get; set; }
	public System.Guid? SourceLineId { get; set; }
	public string? SemanticType { get; set; }
	public decimal? FullMonthAmount { get; set; }
	public string Name { get; set; } = string.Empty;
	public decimal Amount { get; set; }
	public string? Description { get; set; }
	public bool IsFixed { get; set; }
	public bool IsTemp { get; set; }
	public string ComponentType { get; set; } = string.Empty;
	public string DependOn { get; set; } = string.Empty;
}
