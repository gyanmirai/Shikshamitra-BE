using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees;

public interface
	IEmployeeService : ICrudAppService<EmployeeDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateEmployeeDto>
{
}