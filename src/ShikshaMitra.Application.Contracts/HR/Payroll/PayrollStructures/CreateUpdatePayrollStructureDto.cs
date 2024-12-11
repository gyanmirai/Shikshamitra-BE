using System;

namespace ShikshaMitra.HR.Payroll.PayrollStructures;

public class CreateUpdatePayrollStructureDto
{
	public Guid EducationMediumStageId { get; set; }
	public Guid PayElementId { get; set; }
	public PayrollStructureType PayrollStructureType { get; set; }
	public double Amount { get; set; }
}