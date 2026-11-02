using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.WorkShifts;

public interface IEmployeeWorkShiftAppService : IApplicationService
{
	Task<PagedResultDto<EmployeeWorkShiftDto>> GetListAsync(PagedAndSortedResultRequestDto input);
	Task<WorkShiftOptionsDto> LookupAsync();
	Task<EmployeeWorkShiftDto> CreateAsync(SaveWorkShiftInput input);
	Task<EmployeeWorkShiftDto> UpdateAsync(Guid id, SaveWorkShiftInput input);
	Task DeleteAsync(Guid id, string concurrencyStamp);
}
public interface IEmployeeShiftAssignmentAppService : IApplicationService
{
	Task<EmployeeWorkScheduleDto> GetAsync(Guid employeeId);
	Task<EmployeeWorkScheduleDto> ScheduleAsync(Guid employeeId, ScheduleEmployeeShiftInput input);
	Task<EmployeeWorkScheduleDto> CancelAsync(Guid employeeId, Guid assignmentId, string concurrencyStamp);
}