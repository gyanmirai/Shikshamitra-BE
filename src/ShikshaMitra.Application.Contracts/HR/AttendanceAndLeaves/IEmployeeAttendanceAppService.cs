using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees;

public interface IEmployeeAttendanceAppService : ICrudAppService<EmployeeAttendanceDto, Guid, PagedAndSortedResultRequestDto,
	CreateUpdateEmployeeAttendanceDto>
{
	Task<PagedResultDto<EmployeeAttendanceDto>> GetEmployeesByDate(DateTimeOffset date);

	Task<List<EmployeeAttendanceSummaryDto>> GetEmployeeAttendanceSummary(List<Guid> employeeIds,
		DateTime month);
}