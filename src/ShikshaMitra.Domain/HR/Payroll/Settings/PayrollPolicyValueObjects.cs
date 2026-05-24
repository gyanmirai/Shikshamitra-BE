using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Payroll.Settings;

public sealed record DayCalculationManualMonth(int Year, int Month, int ManualDays);

public sealed record PayrollHolidayOverride(
	DateTime Date,
	string HolidayName,
	bool IncludedInPayroll,
	Guid? CalendarHolidayId = null);

public sealed record PayrollLockedMonthSetting(
	int Year,
	int Month,
	DayCalculationType DayCalculationType,
	int? ManualDays,
	SaturdayWorkPolicy SaturdayWorkPolicy,
	IReadOnlyList<PayrollHolidayOverride> HolidayOverrides);
