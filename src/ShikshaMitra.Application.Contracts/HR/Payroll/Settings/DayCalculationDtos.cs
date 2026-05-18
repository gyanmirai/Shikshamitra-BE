using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Payroll.Settings;

public class DayCalculationRequestDto
{
	public Guid? AcademicYearId { get; set; }
	public string DayCalculationType { get; set; } = string.Empty;
	public string SaturdayWorkPolicy { get; set; } = "full-day";
	public string HolidaySyncMode { get; set; } = "synced-with-overrides";
	public int? ManualDays { get; set; }
	public List<DayCalculationManualMonthDto> ManualMonthDays { get; set; } = new();
	public List<PayrollHolidayOverrideDto> HolidayOverrides { get; set; } = new();
}

public class DayCalculationResponseDto
{
	public bool Success { get; set; }
	public string Message { get; set; } = string.Empty;
	public DayCalculationSettingDto? Setting { get; set; }
}

public class DayCalculationManualMonthDto
{
	public int Year { get; set; }
	public int Month { get; set; }
	public int ManualDays { get; set; }
}

public class DayCalculationSettingDto
{
	public Guid? AcademicYearId { get; set; }
	public string DayCalculationType { get; set; } = string.Empty;
	public string SaturdayWorkPolicy { get; set; } = "full-day";
	public string HolidaySyncMode { get; set; } = "synced-with-overrides";
	public int? ManualDays { get; set; }
	public List<DayCalculationManualMonthDto> ManualMonthDays { get; set; } = new();
	public List<PayrollHolidayOverrideDto> HolidayOverrides { get; set; } = new();
	public bool UsedFallbackSetting { get; set; }
	public List<DayCalculationMonthPreviewDto> Months { get; set; } = new();
}

public class DayCalculationMonthPreviewDto
{
	public int Year { get; set; }
	public int Month { get; set; }
	public int CalendarDays { get; set; }
	public int Sundays { get; set; }
	public int Saturdays { get; set; }
	public decimal SaturdayUnits { get; set; }
	public decimal WeeklyOffDays { get; set; }
	public int HolidayDays { get; set; }
	public int PayrollHolidayDays { get; set; }
	public decimal PayrollHolidayUnits { get; set; }
	public decimal WorkingDays { get; set; }
	public int? ManualDays { get; set; }
	public decimal BasisDays { get; set; }
	public string DayCalculationType { get; set; } = string.Empty;
	public bool Locked { get; set; }
	public List<string> HolidayNames { get; set; } = new();
	public List<PayrollResolvedHolidayDto> Holidays { get; set; } = new();
}

public class PayrollHolidayOverrideDto
{
	public DateTime Date { get; set; }
	public string HolidayName { get; set; } = string.Empty;
	public bool IncludedInPayroll { get; set; }
	public Guid? CalendarHolidayId { get; set; }
}

public class PayrollResolvedHolidayDto
{
	public DateTime Date { get; set; }
	public Guid? CalendarHolidayId { get; set; }
	public string HolidayName { get; set; } = string.Empty;
	public string Source { get; set; } = string.Empty;
	public bool IncludedInPayroll { get; set; }
	public bool IsEditable { get; set; }
}
