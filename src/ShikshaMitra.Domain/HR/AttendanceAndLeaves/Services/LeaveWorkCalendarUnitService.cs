using System;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Policies;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeaveWorkCalendarUnitService(
	HrPolicyResolver hrPolicyResolver,
	PayrollCalendarResolver payrollCalendarResolver) : DomainService
{
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

		var total = 0m;
		var cursor = new DateTime(startDate.Year, startDate.Month, 1);
		var lastMonth = new DateTime(endDate.Year, endDate.Month, 1);
		while (cursor <= lastMonth)
		{
			var year = cursor.Year;
			var month = cursor.Month;
			PayrollCalendarResolution calendar;
			HrResolvedPolicies resolvedPolicies;
			try
			{
				calendar = await payrollCalendarResolver.ResolveAsync(year, month, academicYearId, cancellationToken);
				resolvedPolicies = await hrPolicyResolver.ResolveAsync(
					employeeId,
					PayrollRunRules.GetPayrollPolicyResolutionDate(year, month),
					calendar.AcademicYearId,
					cancellationToken);
			}
			catch (BusinessException ex) when (ex.Code == HRErrorCodes.DefaultPayrollPolicyRequired)
			{
				return LeaveDurationCalculator.CountLeaveUnits(startDate, endDate, halfDay);
			}

			var workingUnits = PayrollMonthCalendar.BuildWorkingUnitsByDate(
				year,
				month,
				calendar.Holidays,
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
}
