using ShikshaMitra.HR.Payroll.Settings;
using System;

namespace ShikshaMitra.HR.Payroll.PayrollStructures;

public class CreateUpdatePayrollStructureDto
{
	//public Guid EducationMediumStageId { get; set; }
	public Guid EmployeeId { get; set; }
	public Guid PayElementId { get; set; }
	public PayElementType PayElementType { get; set; }
	public double Amount { get; set; }
}