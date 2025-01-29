using ShikshaMitra.HR.Employees;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Volo.Abp.Domain.Repositories;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.Payroll.PayrollStructures;

public class PayrollStructureManager(
	IPayrollStructureRepository payrollStructureRepository,
	IRepository<Employee, Guid> employeeRepository) : DomainService
{
	public async Task SetEmployeePayrollStructureAsync(Guid employeeId, Guid payrollStructureId)
	{
		var employee = await employeeRepository.GetAsync(employeeId) ?? throw new UserFriendlyException("Employee not found");
		PayrollStructure? payrollStructure = await payrollStructureRepository.GetAsync(payrollStructureId) ?? throw new UserFriendlyException("Payroll structure not found");

		employee.SetPayrollStructure(payrollStructureId);
		await employeeRepository.UpdateAsync(employee);
	}

	public async Task RemoveEmployeePayrollStructureAsync(Guid employeeId)
	{
		var employee = await employeeRepository.GetAsync(employeeId) ?? throw new UserFriendlyException("Employee not found");

		employee.RemovePayrollStructure();
		await employeeRepository.UpdateAsync(employee);
	}
}
