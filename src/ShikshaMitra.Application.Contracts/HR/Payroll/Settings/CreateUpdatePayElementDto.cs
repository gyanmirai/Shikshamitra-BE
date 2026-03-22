namespace ShikshaMitra.HR.Payroll.Settings;

public class CreateUpdatePayElementDto
{
	public string Code { get; set; }

	public string Name { get; set; }

	public PayElementType Type { get; set; }

	public bool IsEmployeeSpecific { get; set; }
	public bool IsDependent { get; set; }
	public DependentPropertiesDto? Dependent { get; set; }
	public bool IsVisibleInPaySlip { get; set; }
}

public class DependentPropertiesDto
{
	public string ElementId { get; set; }
	public float? Percentage { get; set; }
	public float? Amount { get; set; }
}
