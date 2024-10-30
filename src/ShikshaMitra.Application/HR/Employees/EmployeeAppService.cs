using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees;

public class EmployeeAppService(IEmployeeRepository employeeRepository) :
	CrudAppService<Employee, EmployeeDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateEmployeeDto>(
		employeeRepository),
	IEmployeeService
{
	public async Task<EmployeeDto?> GetEmployeeByUserId(Guid userId)
	{
		Employee? employee = await employeeRepository.GetEmployeeByUserIdAsync(userId);

		return ObjectMapper.Map<Employee, EmployeeDto>(employee);
	}
}