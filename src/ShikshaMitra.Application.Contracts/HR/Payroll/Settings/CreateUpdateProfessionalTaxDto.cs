using System;

namespace ShikshaMitra.HR.Payroll.Settings;

public class CreateUpdateProfessionalTaxDto
{
	public string JurisdictionCode { get; set; } = "IN-GJ";
	public DateTime EffectiveFrom { get; set; } = new(2022, 4, 1);
	public DateTime? EffectiveTo { get; set; }
	public decimal FromSalary { get; set; }
	public decimal? ToSalary { get; set; }
	public decimal TaxAmount { get; set; }
	public bool IsActive { get; set; } = true;
	public bool DeductInCessationMonth { get; set; } = true;
}
