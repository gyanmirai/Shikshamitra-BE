using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Policies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public class SalarySlipGenerationDataReader(
	IRepository<EmployeeAttendance, Guid> attendanceRepository,
	IRepository<PayrollAdjustment, Guid> payrollAdjustmentRepository,
	IRepository<WorkCalendarHoliday, Guid> workCalendarHolidayRepository)
	: DomainService
{
	public async Task<List<EmployeeAttendance>> GetAttendanceForEmployeeMonthAsync(
		Guid employeeId,
		DateOnly startDate,
		DateOnly endDate)
	{
		var attendanceQuery = await attendanceRepository.GetQueryableAsync();

		return await AsyncExecuter.ToListAsync(
			attendanceQuery.Where(a =>
				a.EmployeeId == employeeId &&
				a.AttendanceDate >= startDate &&
				a.AttendanceDate <= endDate));
	}

	public async Task<List<PayrollAdjustment>> GetAdjustmentsForEmployeeMonthAsync(
		Guid employeeId,
		string monthYear,
		CancellationToken cancellationToken)
	{
		var query = await payrollAdjustmentRepository.GetQueryableAsync();

		return await AsyncExecuter.ToListAsync(
			query
				.Where(x => x.EmployeeId == employeeId && x.MonthYear == monthYear)
				.OrderBy(x => x.CreationTime),
			cancellationToken);
	}

	public async Task<List<Holiday>> GetIncludedWorkCalendarHolidaysAsync(
		Guid workCalendarPolicyId,
		int year,
		int month,
		CancellationToken cancellationToken)
	{
		var start = new DateOnly(year, month, 1);
		var end = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
		var query = await workCalendarHolidayRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(query.Where(x =>
			x.WorkCalendarPolicyId == workCalendarPolicyId &&
			x.IsIncluded &&
			x.ToDate >= start &&
			x.FromDate <= end), cancellationToken);
		return rows
			.OrderBy(x => x.FromDate)
			.ThenBy(x => x.HolidayName)
			.Select(x => x.ToCalendarHoliday(CurrentTenant.Id))
			.ToList();
	}
}
