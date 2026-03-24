using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees.Payroll;

/// <summary>
/// Application service for managing employee-level payroll details and contract.
/// </summary>
public interface IEmployeePayrollAppService : IApplicationService
{
	Task<EmployeePayrollDetailsDto> GetPayrollDetailsAsync(Guid employeeId);
	Task UpdatePayrollDetailsAsync(Guid employeeId, UpdateEmployeePayrollDetailsDto input);
	Task<PayrollContract> GetPayrollContractAsync(Guid employeeId);
	Task UpdatePayrollContractAsync(Guid employeeId, PayrollContract input);
	Task SetPayrollStructureAsync(Guid employeeId, Guid payrollStructureId);
	Task RemovePayrollStructureAsync(Guid employeeId);
	Task UpdatePayrollStructureAsync(Guid employeeId, CreateUpdateEmployeePayrollStructureDto input);
}
