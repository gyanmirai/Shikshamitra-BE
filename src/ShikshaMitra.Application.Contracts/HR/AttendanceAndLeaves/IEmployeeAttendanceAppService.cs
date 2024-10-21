using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees;

public interface IEmployeeAttendanceAppService : ICrudAppService<EmployeeAttendanceDto, Guid, PagedAndSortedResultRequestDto,
	CreateUpdateEmployeeAttendanceDto>
{
	Task<PagedResultDto<EmployeeAttendanceDto>> GetEmployeesByDateAsync(DateTime date);

	Task<List<EmployeeAttendanceSummaryDto>> GetEmployeeAttendanceSummaryAsync(List<Guid> employeeIds,
		DateTime month);
}