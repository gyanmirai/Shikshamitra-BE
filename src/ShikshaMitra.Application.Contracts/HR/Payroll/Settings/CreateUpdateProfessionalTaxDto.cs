using ShikshaMitra.Common;

namespace ShikshaMitra.HR.Payroll.Settings;

public class CreateUpdateProfessionalTaxDto
{
	public GenderType GenderType { get;  set; }
	public double FromSalary { get;  set; }
	public double ToSalary { get;  set; }
	public double TaxAmount { get;  set; }
}
