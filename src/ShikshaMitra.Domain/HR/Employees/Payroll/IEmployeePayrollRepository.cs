using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Employees.Payroll;

public interface IEmployeePayrollDetailsRepository : IRepository<EmployeePayrollDetails, Guid>
{
	Task<EmployeePayrollDetails?> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
}

public interface IEmployeePayrollComponentRepository : IRepository<EmployeePayrollComponent, Guid>
{
	Task<List<EmployeePayrollComponent>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
	
	Task<List<EmployeePayrollComponent>> GetEarningsByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
	
	Task<List<EmployeePayrollComponent>> GetDeductionsByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
	
	Task DeleteByEmployeeIdAsync(Guid employeeId, bool isEarning, CancellationToken cancellationToken = default);
}
