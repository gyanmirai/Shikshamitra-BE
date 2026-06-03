using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Payroll.Settings;

public class PayElementDto : AuditedEntityDto<Guid>
{
	public string Code { get; set; } = string.Empty; // Unique code for the PayElement
	public string Name { get; set; } = string.Empty; // Name of the PayElement
	public PayElementType Type { get; set; } // Type of the PayElement
	public bool IsEmployeeSpecific { get; set; } // Whether it is employee-specific
	public bool IsDependent { get; set; } // Whether it is dependent on another element
	public DependentPropertiesDto? Dependent { get; set; } // Dependent properties
	public bool IsVisibleInPaySlip { get; set; } // Whether it is visible in the pay slip
}
