using System;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.Employees;

public class EmployeeLookupReader(IEmployeeRepository employeeRepository) : DomainService
{
	public async Task<Employee> GetRequiredAsync(Guid employeeId)
	{
		return await employeeRepository.FindAsync(employeeId, includeDetails: false)
			?? throw new BusinessException(HRErrorCodes.EmployeeNotFound);
	}
}
