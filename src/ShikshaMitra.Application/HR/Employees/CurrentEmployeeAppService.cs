using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.HR.Employees;

[RemoteService(false)]
[Authorize]
public class CurrentEmployeeAppService(IEmployeeRepository employeeRepository)
	: ApplicationService, ICurrentEmployeeAppService
{
	public async Task<EmployeeDto> GetCurrentAsync()
	{
		if (!CurrentUser.Id.HasValue)
		{
			throw new EntityNotFoundException(typeof(Employee), Guid.Empty);
		}

		var employee = await employeeRepository.GetEmployeeByUserIdAsync(CurrentUser.Id.Value)
			?? throw new EntityNotFoundException(typeof(Employee), CurrentUser.Id.Value);

		return ObjectMapper.Map<Employee, EmployeeDto>(employee);
	}
}
