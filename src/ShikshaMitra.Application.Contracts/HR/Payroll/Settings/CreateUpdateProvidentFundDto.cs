using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Payroll.Settings;

public class CreateUpdateProvidentFundDto
{
	[Required] [MaxLength(50)] public string Code { get; set; }
	[Required] [MaxLength(100)] public string Name { get; set; }
	[Required] public float EmployeePercentage { get; set; }
	[Required] public float EmployerPercentage { get; set; }
}