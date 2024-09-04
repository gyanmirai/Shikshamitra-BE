using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees;

public class EmployeeAppService(IEmployeeRepository employeeRepository) :
	CrudAppService<Employee, EmployeeDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateEmployeeDto>(
		employeeRepository),
	IEmployeeService
{
}