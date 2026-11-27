using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class AttendanceRegisterLookupReader(
	IRepository<EducationMediumStage, Guid> mediumStageRepository,
	IStaffAttendanceDayLockRepository staffAttendanceDayLockRepository,
	IRepository<SalarySlip, Guid> salarySlipRepository,
	IRepository<PayrollRunItemSnapshot, Guid> payrollRunItemSnapshotRepository,
	IRepository<Holiday, Guid> holidayRepository,
	IRepository<WorkCalendarHoliday, Guid> workCalendarHolidayRepository,
	HrAttendanceWorkCalendarResolver attendanceWorkCalendarResolver,
	IRepository<Employee, Guid> employeeRepository,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	internal async Task<PayrollLockInfo> ResolvePayrollLockAsync(Guid employeeId, string monthYear)
	{
		var lookup = await BuildLockLookupAsync([new RegisterProjection
		{
			EmployeeId = employeeId,
			AttendanceDate = AttendanceRegisterProjection.ParseMonthDate(monthYear)
		}]);

		return lookup.GetValueOrDefault((employeeId, monthYear), new PayrollLockInfo(false, null));
	}

	internal async Task<Dictionary<DateOnly, HolidayInfo>> BuildHolidayLookupAsync(IEnumerable<DateOnly> dates)
	{
		var dateList = dates.Distinct().OrderBy(x => x).ToList();
		if (dateList.Count == 0)
		{
			return new Dictionary<DateOnly, HolidayInfo>();
		}

		var start = dateList.First();
		var end = dateList.Last();
		var query = await holidayRepository.GetQueryableAsync();
		var holidays = await asyncExecuter.ToListAsync(query
			.Where(x => x.StartDate <= end && x.EndDate >= start));
		var lookup = new Dictionary<DateOnly, HolidayInfo>();
		var requestedDates = dateList.ToHashSet();

		foreach (var holiday in holidays.OrderBy(x => x.StartDate))
		{
			var first = Math.Max(start.DayNumber, holiday.StartDate.DayNumber);
			var last = Math.Min(end.DayNumber, holiday.EndDate.DayNumber);
			for (var day = first; day <= last; day++)
			{
				var date = DateOnly.FromDayNumber(day);
				if (requestedDates.Contains(date))
					lookup.TryAdd(date, new HolidayInfo(holiday.HolidayName));
			}
		}

		return lookup;
	}

	internal async Task<Dictionary<(Guid EmployeeId, DateOnly Date), HolidayInfo>> BuildEmployeeHolidayLookupAsync(
		IEnumerable<Guid> employeeIds,
		IEnumerable<DateOnly> dates,
		bool includeWeeklyOffs = false)
	{
		var distinctEmployeeIds = employeeIds.Distinct().ToList();
		var dateList = dates.Distinct().OrderBy(x => x).ToList();
		if (distinctEmployeeIds.Count == 0 || dateList.Count == 0)
		{
			return new Dictionary<(Guid EmployeeId, DateOnly Date), HolidayInfo>();
		}

		var result = new Dictionary<(Guid EmployeeId, DateOnly Date), HolidayInfo>();
		var start = dateList.First();
		var end = dateList.Last();
		var centralHolidayLookup = await BuildHolidayLookupAsync(dateList);
		var query = await workCalendarHolidayRepository.GetQueryableAsync();
		var staff = await asyncExecuter.ToListAsync((await employeeRepository.GetQueryableAsync()).Where(x => distinctEmployeeIds.Contains(x.Id)));
		using var batch = await attendanceWorkCalendarResolver.BeginReadBatchAsync(staff, start, end);
		var holidaysByCalendar = new Dictionary<Guid, Dictionary<DateOnly, HolidayInfo>>();

		foreach (var employeeId in distinctEmployeeIds)
		{
			foreach (var date in dateList)
			{
				WorkCalendarPolicy workCalendar;
				try
				{
					workCalendar = await attendanceWorkCalendarResolver.ResolveAsync(employeeId, date.ToDateTime(TimeOnly.MinValue));
				}
				catch (Exception exception) when (exception is BusinessException or Volo.Abp.Domain.Entities.EntityNotFoundException)
				{
					if (includeWeeklyOffs) throw;
					if (centralHolidayLookup.TryGetValue(date, out var fallbackHoliday))
					{
						result.TryAdd((employeeId, date), fallbackHoliday);
					}
					continue;
				}

				var calendarId = workCalendar.Id;
				if (includeWeeklyOffs && ShikshaMitra.HR.Payroll.Services.PayrollMonthCalendar.IsWeeklyOff(date.DayOfWeek, workCalendar.WeeklyOffPattern, workCalendar.GetCustomWeeklyOffs()))
				{
					result.TryAdd((employeeId, date), new HolidayInfo("Weekly off"));
				}
				if (!holidaysByCalendar.TryGetValue(calendarId, out var holidays))
				{
					var calendarHolidays = await asyncExecuter.ToListAsync(query.Where(x =>
						x.WorkCalendarPolicyId == calendarId &&
						x.IsIncluded &&
						x.ToDate >= start &&
						x.FromDate <= end));
					holidays = new Dictionary<DateOnly, HolidayInfo>();
					foreach (var holiday in calendarHolidays.OrderBy(x => x.FromDate).ThenBy(x => x.HolidayName))
					{
						var first = Math.Max(start.DayNumber, holiday.FromDate.DayNumber);
						var last = Math.Min(end.DayNumber, holiday.ToDate.DayNumber);
						var info = new HolidayInfo(holiday.HolidayName);
						for (var day = first; day <= last; day++)
							holidays.TryAdd(DateOnly.FromDayNumber(day), info);
					}
					holidaysByCalendar[calendarId] = holidays;
				}
				if (holidays.TryGetValue(date, out var calendarHoliday))
					result.TryAdd((employeeId, date), calendarHoliday);
			}
		}

		return result;
	}

	internal Task<Dictionary<(Guid EmployeeId, string MonthYear), PayrollLockInfo>> BuildLockLookupAsync(
		List<RegisterProjection> rows) =>
		BuildLockLookupAsync(rows.Select(x => (x.EmployeeId, x.AttendanceDate)));

	internal async Task<Dictionary<(Guid EmployeeId, string MonthYear), PayrollLockInfo>> BuildLockLookupAsync(
		IEnumerable<(Guid EmployeeId, DateOnly AttendanceDate)> attendance)
	{
		var keys = attendance.Select(x => (x.EmployeeId, MonthYear: AttendanceRegisterProjection.ToMonthYear(x.AttendanceDate))).Distinct().ToList();
		var result = keys.ToDictionary(x => x, _ => new PayrollLockInfo(false, null));
		if (keys.Count == 0)
		{
			return result;
		}

		var employeeIds = keys.Select(x => x.EmployeeId).Distinct().ToList();
		var months = keys.Select(x => x.MonthYear).Distinct().ToList();
		var slipQuery = await salarySlipRepository.GetQueryableAsync();
		var snapshotQuery = await payrollRunItemSnapshotRepository.GetQueryableAsync();

		var finalizedSlips = await asyncExecuter.ToListAsync(
			slipQuery.Where(x =>
				employeeIds.Contains(x.EmployeeId) &&
				months.Contains(x.MonthYear) &&
				(x.Status == SalarySlipStatus.Finalized || x.Status == SalarySlipStatus.Paid))
			.Select(x => new { x.EmployeeId, x.MonthYear }));
		foreach (var slip in finalizedSlips)
		{
			var key = (slip.EmployeeId, slip.MonthYear);
			if (result.ContainsKey(key))
				result[key] = new PayrollLockInfo(true, $"Payroll finalized for {slip.MonthYear}.");
		}

		var lockedSnapshots = await asyncExecuter.ToListAsync(
			snapshotQuery.Where(x =>
				employeeIds.Contains(x.EmployeeId) &&
				months.Contains(x.MonthYear) &&
				x.IsLocked)
			.Select(x => new { x.EmployeeId, x.MonthYear }));
		foreach (var snapshot in lockedSnapshots)
		{
			var key = (snapshot.EmployeeId, snapshot.MonthYear);
			if (result.ContainsKey(key))
				result[key] = new PayrollLockInfo(true, $"Payroll locked for {snapshot.MonthYear}.");
		}

		return result;
	}

	internal async Task<Dictionary<Guid, string>> BuildStageNameLookupAsync(IEnumerable<Guid> stageIds)
	{
		var ids = stageIds.Distinct().ToList();
		if (ids.Count == 0)
		{
			return new Dictionary<Guid, string>();
		}

		var query = await mediumStageRepository.GetQueryableAsync();
		return (await asyncExecuter.ToListAsync(query.Where(x => ids.Contains(x.Id))))
			.ToDictionary(x => x.Id, x => x.Name);
	}

	internal async Task<HashSet<DateOnly>> BuildDayLockLookupAsync(IEnumerable<DateOnly> dates)
	{
		var requestedDates = dates.Distinct().ToArray();
		if (requestedDates.Length == 0)
		{
			return [];
		}

		var query = await staffAttendanceDayLockRepository.GetQueryableAsync();
		var lockedDates = await asyncExecuter.ToListAsync(query
			.Where(x => Enumerable.Contains(requestedDates, x.LockDate))
			.Select(x => x.LockDate));
		return lockedDates.ToHashSet();
	}
}
