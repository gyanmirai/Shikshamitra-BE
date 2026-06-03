using System;
using ShikshaMitra.HR.Payroll.Settings;

namespace ShikshaMitra.HR.Payroll.Services;

public static class PayrollDayCalculationApiLabels
{
	public static string ToApiString(DayCalculationType type) => type switch
	{
		DayCalculationType.WorkingDays => "working-days",
		DayCalculationType.TotalMonthDays => "total-month-days",
		DayCalculationType.ManualDays => "manual-days",
		_ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
	};
}
