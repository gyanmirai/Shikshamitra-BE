using ShikshaMitra.Common;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Payroll.Settings;

public class CreateUpdateProfessionalTaxDto
{
	[Required] public GenderType GenderType { get;  set; }
	public double FromSalary { get;  set; }
	public double ToSalary { get;  set; }
	public double TaxAmount { get;  set; }
}