using ShikshaMitra.HR.Payroll.PayrollStructures;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Employees;

public  class CreateUpdateEmployeePayrollStructureDto
{
	public List<PayrollStructureDto> PayrollStructures { get; set; }
}
