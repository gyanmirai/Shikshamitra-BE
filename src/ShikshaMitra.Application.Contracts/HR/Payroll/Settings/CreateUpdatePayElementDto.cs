namespace ShikshaMitra.HR.Payroll.Settings;

public class CreateUpdatePayElementDto
{
	public string Code { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	public PayElementType Type { get; set; }

	public bool IsEmployeeSpecific { get; set; }
	public bool IsDependent { get; set; }
	public DependentPropertiesDto? Dependent { get; set; }
	public bool IsVisibleInPaySlip { get; set; }
}

public class DependentPropertiesDto
{
	public string ElementId { get; set; } = string.Empty;
	public float? Percentage { get; set; }
	public float? Amount { get; set; }
}
