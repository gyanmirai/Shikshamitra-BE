using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Payroll.SalaryStructure;

public class EmployeeSalaryStructureReadService(IEmployeePayrollComponentRepository payrollComponentRepository)
	: ApplicationService, ITransientDependency
{
	public async Task<SalaryStructureResponseDto> GetAsync(Guid employeeId)
	{
		var earnings = await payrollComponentRepository.GetEarningsByEmployeeIdAsync(employeeId);
		var deductions = await payrollComponentRepository.GetDeductionsByEmployeeIdAsync(employeeId);

		return new SalaryStructureResponseDto
		{
			EarningComponents = ObjectMapper.Map<List<EmployeePayrollComponent>, List<SalaryComponentDto>>(earnings.ToList()),
			DeductionComponents = ObjectMapper.Map<List<EmployeePayrollComponent>, List<SalaryComponentDto>>(deductions.ToList())
		};
	}
}
