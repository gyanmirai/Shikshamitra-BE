using ShikshaMitra.HR.WorkShifts;
using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR;
using ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Policies;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Timing;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class TakeAttendanceDayReader(
	IEmployeeAttendanceRepository employeeAttendanceRepository,
	IEmployeeLeaveRepository employeeLeaveRepository,
	IStaffAttendanceDayLockRepository staffAttendanceDayLockRepository,
	TakeAttendanceAccessService accessService,
	TakeAttendanceHolidayReader holidayReader,
	HrEmployeeScopeQueryService hrEmployeeScopeQueryService,
	EmployeeShiftResolver shiftResolver,
	AttendanceCaptureModeLookupResolver captureModeLookupResolver,
	IClock clock,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public async Task<TakeAttendanceDayResponse> GetDayAsync(GetTakeAttendanceDayInput input)
	{
		var readPage = await PreparePagesAsync(input);
		return await readPage(input.SkipCount, input.MaxResultCount <= 0 ? 50 : input.MaxResultCount);
	}

	internal async Task<Func<int, int, Task<TakeAttendanceDayResponse>>> PreparePagesAsync(GetTakeAttendanceDayInput input)
	{
		var date = TakeAttendanceDateParser.ParseDate(input.AttendanceDate);
		var dateOnly = DateOnly.FromDateTime(date);
		if (date.Date > clock.Now.Date)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceFutureDateNotAllowed);
		}

		var scope = await accessService.EnsureCanOperateAttendanceAsync();
		var locked = await staffAttendanceDayLockRepository.FindAsync(dateOnly);
		var holiday = await holidayReader.FindHolidayAsync(dateOnly);
		var holidayReason = holiday == null ? null : $"Holiday: {holiday.HolidayName}";

		var empQuery = await hrEmployeeScopeQueryService.BuildScopedEmployeeQueryAsync(
			scope,
			new HrEmployeeScopeOptions
			{
				ActiveOnOrAfter = dateOnly,
				JoinedOnOrBefore = dateOnly
			});

		if (!string.IsNullOrWhiteSpace(input.Department))
		{
			var d = input.Department.Trim().ToLowerInvariant();
			empQuery = empQuery.Where(e => e.Department != null && e.Department.ToLower().Contains(d));
		}

		if (input.IsTeaching.HasValue)
		{
			empQuery = input.IsTeaching.Value
				? empQuery.Where(e => e.EmployeeGroup == EmployeeGroupType.Teaching)
				: empQuery.Where(e => e.EmployeeGroup != EmployeeGroupType.Teaching);
		}

		if (input.RoleId.HasValue)
		{
			empQuery = empQuery.Where(e => e.RoleId == input.RoleId.Value);
		}

		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var s = input.Search.Trim().ToLowerInvariant();
			empQuery = empQuery.Where(e =>
				(((e.FirstName ?? "") + " " + (e.MiddleName ?? "")).Trim() + " " + (e.LastName ?? "")).Trim().ToLower().Contains(s) ||
				(e.EmployeeCode ?? "").ToLower().Contains(s) ||
				(e.MobileNo ?? "").ToLower().Contains(s));
		}

		// The scoped employee query already requires an accessible assignment for narrow scopes.
		var employeeIds = empQuery.Select(e => e.Id);
		var totalCount = await asyncExecuter.CountAsync(employeeIds);
		var attendanceQuery = (await employeeAttendanceRepository.GetQueryableAsync())
			.Where(a => a.AttendanceDate == dateOnly);
		var approvedLeaveQuery = (await employeeLeaveRepository.GetQueryableAsync())
			.Where(l => l.Status == EmployeeLeaveStatusType.Approved &&
				l.StartDateOnly <= dateOnly && l.EndDateOnly >= dateOnly);

		var summary = new TakeAttendanceSummaryDto();
		if (totalCount > 0)
		{
			var scopedAttendance = attendanceQuery.Where(a => employeeIds.Contains(a.EmployeeId));
			var statusCounts = await asyncExecuter.ToListAsync(scopedAttendance
				.GroupBy(a => a.StatusType)
				.Select(g => new { Status = g.Key, Count = g.Count() }));
			var markedIds = scopedAttendance.Select(a => a.EmployeeId);
			summary.Present = statusCounts.FirstOrDefault(x => x.Status == EmployeeAttendanceStatusType.Present)?.Count ?? 0;
			summary.HalfDay = statusCounts.FirstOrDefault(x => x.Status == EmployeeAttendanceStatusType.HalfDayLeave)?.Count ?? 0;
			summary.Absent = statusCounts.FirstOrDefault(x => x.Status == EmployeeAttendanceStatusType.FullDayLeave)?.Count ?? 0;
			summary.OnLeave = await asyncExecuter.CountAsync(approvedLeaveQuery
				.Where(l => employeeIds.Contains(l.EmployeeId) && !markedIds.Contains(l.EmployeeId))
				.Select(l => l.EmployeeId)
				.Distinct());
			summary.NotMarked = totalCount - statusCounts.Sum(x => x.Count) - summary.OnLeave;
		}

		return async (skip, size) =>
		{
			var response = new TakeAttendanceDayResponse
			{
				TotalCount = totalCount,
				Date = dateOnly,
				IsDayLocked = locked != null,
				IsHoliday = holiday != null,
				HolidayName = holiday?.HolidayName,
				HolidayReason = holidayReason,
				LockedByUserId = locked?.LockedByUserId,
				LockedAt = locked?.LockedAt,
				Summary = summary
			};

			if (skip >= totalCount)
			{
				return response;
			}

			var employees = await asyncExecuter.ToListAsync(empQuery
				.OrderBy(e => e.FirstName).ThenBy(e => e.LastName).ThenBy(e => e.Id)
				.Skip(skip).Take(size)
				.Select(e => new TakeAttendanceDayProjection.EmployeeRow(
					e.Id, e.EmployeeGroup, e.FirstName, e.MiddleName, e.LastName, e.EmployeeCode, e.Department)));

			var ids = employees.Select(e => e.Id).ToList();
			var attendanceRows = await asyncExecuter.ToListAsync(attendanceQuery
				.Where(a => ids.Contains(a.EmployeeId))
				.Select(a => new TakeAttendanceDayProjection.AttendanceRow(
					a.EmployeeId, a.StatusType, a.TimeIn, a.TimeOut, a.Remarks, a.Source)));
			var attendanceByEmployee = attendanceRows.GroupBy(a => a.EmployeeId)
				.ToDictionary(g => g.Key, g => g.First());
			var approvedLeaves = await asyncExecuter.ToListAsync(approvedLeaveQuery
				.Where(l => ids.Contains(l.EmployeeId))
				.Select(l => new ApprovedAttendanceLeaveRow(l.Id, l.EmployeeId, l.HalfDay, l.StartDate, l.EndDate)));
			var leaveByEmployee = approvedLeaves.GroupBy(l => l.EmployeeId)
				.ToDictionary(g => g.Key, g => g.OrderBy(l => l.StartDate).ThenBy(l => l.LeaveId).First());
			var shifts = await shiftResolver.ForDateAsync(ids, dateOnly);
			var captureModes = await captureModeLookupResolver.ResolveForDateOrDefaultAsync(ids, dateOnly);

			response.Items = TakeAttendanceDayProjection.BuildRows(employees, attendanceByEmployee, leaveByEmployee,
				shifts, captureModes, date, holiday, holidayReason);
			var distinctCaptureModes = response.Items.Select(x => x.CaptureMode)
				.Distinct(StringComparer.OrdinalIgnoreCase).Take(2).ToList();
			if (distinctCaptureModes.Count == 1)
			{
				response.CaptureMode = distinctCaptureModes[0];
			}

			return response;
		};
	}
}
