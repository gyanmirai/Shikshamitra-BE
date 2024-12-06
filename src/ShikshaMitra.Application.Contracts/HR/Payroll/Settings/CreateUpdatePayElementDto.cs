using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Payroll.Settings;

public class CreateUpdatePayElementDto
{
	[Required] [MaxLength(50)] public string Code { get; set; }

	[Required] [MaxLength(100)] public string Name { get; set; }

	[Required] public PayElementType Type { get; set; }

	public bool IsEmployeeSpecific { get; set; }
	public bool IsDependent { get; set; }
	public DependentPropertiesDto? Dependent { get; set; }
	public bool IsVisibleInPaySlip { get; set; }
}

public class DependentPropertiesDto
{
	public string ElementCode { get; set; } // Code of the dependent element
	public float? Percentage { get; set; } // Percentage of the dependent element
	public float? Amount { get; set; } // Fixed amount of the dependent element
}