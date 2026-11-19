using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Policies;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeaveWorkCalendarUnitService(
	HrPolicyResolver hrPolicyResolver,
	PayrollCalendarResolver payrollCalendarResolver,
	IRepository<WorkCalendarHoliday, Guid> workCalendarHolidayRepository) : DomainService
{
	private Dictionary<string, object>? batch;
	public IDisposable BeginReadBatch()
	{
		var previous = batch; batch = new(); return new Volo.Abp.DisposeAction(() => batch = previous);
	}
	private async Task<T> ReadAsync<T>(string key, Func<Task<T>> read)
	{
		if (batch?.TryGetValue(key, out var value) == true) return (T) value;
		var result = await read(); if (batch != null) batch[key] = result!; return result;
	}
	public async Task<decimal> CalculateUnitsAsync(
		Guid employeeId,
		DateTime startDate,
		DateTime endDate,
		bool halfDay,
		Guid? academicYearId = null,
		CancellationToken cancellationToken = default)
	{
		if (startDate.Date > endDate.Date)
		{
			return 0m;
		}

		if (halfDay && startDate.Date != endDate.Date)
		{
			return 0m;
		}

		var total = 0m;
		var cursor = new DateTime(startDate.Year, startDate.Month, 1);
		var lastMonth = new DateTime(endDate.Year, endDate.Month, 1);
		while (cursor <= lastMonth)
		{
			var year = cursor.Year;
			var month = cursor.Month;
			var calendar = await ReadAsync($"calendar:{year}:{month}:{academicYearId}", () => payrollCalendarResolver.ResolveAsync(year, month, academicYearId, cancellationToken));
			var resolvedPolicies = await hrPolicyResolver.ResolveAsync(
				employeeId,
				PayrollRunRules.GetPayrollPolicyResolutionDate(year, month),
				calendar.AcademicYearId,
				cancellationToken);

			var workCalendarHolidays = await ReadAsync($"holidays:{resolvedPolicies.PayrollWorkCalendarPolicy.Id}:{year}:{month}", () => GetIncludedWorkCalendarHolidaysAsync(
				resolvedPolicies.PayrollWorkCalendarPolicy.Id,
				year,
				month,
				cancellationToken));
			var workingUnits = PayrollMonthCalendar.BuildWorkingUnitsByDate(
				year,
				month,
				workCalendarHolidays,
				resolvedPolicies.PayrollPolicy.SaturdayWorkPolicy,
				resolvedPolicies.PayrollPolicy.GetHolidayOverrides(),
				resolvedPolicies.PayrollWorkCalendarPolicy);

			foreach (var (date, workingUnit) in workingUnits)
			{
				var dateTime = date.ToDateTime(TimeOnly.MinValue);
				if (dateTime < startDate.Date || dateTime > endDate.Date || workingUnit <= 0m)
				{
					continue;
				}

				total += halfDay
					? Math.Min(0.5m, workingUnit)
					: workingUnit;
			}

			cursor = cursor.AddMonths(1);
		}

		return total;
	}

	private async Task<List<Holiday>> GetIncludedWorkCalendarHolidaysAsync(
		Guid workCalendarPolicyId,
		int year,
		int month,
		CancellationToken cancellationToken)
	{
		var start = new DateOnly(year, month, 1);
		var end = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
		var rows = await workCalendarHolidayRepository.GetListAsync(
			x =>
				x.WorkCalendarPolicyId == workCalendarPolicyId &&
				x.IsIncluded &&
				x.ToDate >= start &&
				x.FromDate <= end,
			includeDetails: false,
			cancellationToken);

		return rows
			.OrderBy(x => x.FromDate)
			.ThenBy(x => x.HolidayName)
			.Select(x => x.ToCalendarHoliday(CurrentTenant.Id))
			.ToList();
	}
}