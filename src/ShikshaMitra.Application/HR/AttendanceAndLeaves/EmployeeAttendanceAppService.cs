using ShikshaMitra.HR.AttendanceAndLeaves;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees;

public class EmployeeAttendanceAppService(IEmployeeAttendanceRepository employeeAttendanceRepository)
	: CrudAppService<EmployeeAttendance, EmployeeAttendanceDto, Guid, PagedAndSortedResultRequestDto,
		CreateUpdateEmployeeAttendanceDto>(employeeAttendanceRepository), IEmployeeAttendanceAppService
{
	public async Task<PagedResultDto<EmployeeAttendanceDto>> GetEmployeesByDate(DateTimeOffset date)
	{
		List<EmployeeAttendance> employees =
			await employeeAttendanceRepository.GetListAsync(e => e.CreationTime.Date == date.Date,
				includeDetails: true);

		return new PagedResultDto<EmployeeAttendanceDto>
		{
			TotalCount = employees.Count,
			Items = ObjectMapper.Map<List<EmployeeAttendance>, List<EmployeeAttendanceDto>>(employees)
		};
	}

	public async Task<List<EmployeeAttendanceSummaryDto>> GetEmployeeAttendanceSummary(List<Guid> employeeIds,
		DateTime month)
	{
		var startOfMonth = new DateOnly(month.Year, month.Month, 1);
		var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

		List<EmployeeAttendance> employeeAttendances = await employeeAttendanceRepository.GetListAsync(
			e => employeeIds.Contains(e.EmployeeId) && e.AttendanceDate >= startOfMonth &&
			     e.AttendanceDate <= endOfMonth, includeDetails: true);

		var summary = new List<EmployeeAttendanceSummaryDto>();

		foreach (var employeeId in employeeIds)
		{
			var employeeAttendance = employeeAttendances.Where(e => e.EmployeeId == employeeId).ToList();
			var totalWorkingDays = employeeAttendance.Count(e => e.StatusType == EmployeeAttendanceStatusType.Present);
			var totalFullDayLeaves =
				employeeAttendance.Count(e => e.StatusType == EmployeeAttendanceStatusType.FullDayLeave);
			var totalHalfDayLeaves = employeeAttendance.Count(e => e.StatusType == EmployeeAttendanceStatusType.HalfDayLeave);

			var employee = employeeAttendance.Find(e => e.EmployeeId == employeeId)?.Employee;
			summary.Add(new EmployeeAttendanceSummaryDto
			{
				EmployeeId = employeeId,
				EmployeeName = employee?.EmployeeName ?? string.Empty,
				TotalWorkingDays = totalWorkingDays + totalFullDayLeaves + totalHalfDayLeaves,
				PresentDays = totalWorkingDays,
				LeaveDays = totalFullDayLeaves,
				HalfDayLeaves = totalHalfDayLeaves,
				EmployeeAttendances = new PagedResultDto<EmployeeAttendanceDto>
				{
					TotalCount = employeeAttendance.Count,
					Items = ObjectMapper.Map<List<EmployeeAttendance>, List<EmployeeAttendanceDto>>(
						employeeAttendance)
				}
			});
		}

		return summary;
	}
}