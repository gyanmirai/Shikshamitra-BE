using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Payroll.Settings;

public class DayCalculationRequestDto
{
	public Guid? AcademicYearId { get; set; }
	public string DayCalculationType { get; set; } = string.Empty;
	public int? ManualDays { get; set; }
	public List<DayCalculationManualMonthDto> ManualMonthDays { get; set; } = new();
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
	public int? ManualDays { get; set; }
	public List<DayCalculationManualMonthDto> ManualMonthDays { get; set; } = new();
	public bool UsedFallbackSetting { get; set; }
	public List<DayCalculationMonthPreviewDto> Months { get; set; } = new();
}

public class DayCalculationMonthPreviewDto
{
	public int Year { get; set; }
	public int Month { get; set; }
	public int CalendarDays { get; set; }
	public int Sundays { get; set; }
	public int HolidayDays { get; set; }
	public int WorkingDays { get; set; }
	public int? ManualDays { get; set; }
	public int BasisDays { get; set; }
	public string DayCalculationType { get; set; } = string.Empty;
	public List<string> HolidayNames { get; set; } = new();
}
