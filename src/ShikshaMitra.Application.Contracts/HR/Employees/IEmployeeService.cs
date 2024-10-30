using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees;

public interface
	IEmployeeService : ICrudAppService<EmployeeDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateEmployeeDto>
{
	Task<EmployeeDto?> GetEmployeeByUserId(Guid userId);
}