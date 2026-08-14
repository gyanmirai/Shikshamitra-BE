using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Payroll.Settings;

public class ProfessionalTaxDto : AuditedEntityDto<Guid>
{
	public string JurisdictionCode { get; set; } = "IN-GJ";
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public decimal FromSalary { get; set; }
	public decimal? ToSalary { get; set; }
	public decimal TaxAmount { get; set; }
	public bool IsActive { get; set; }
	public bool DeductInCessationMonth { get; set; }
	public int Version { get; set; }
}
