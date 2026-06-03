using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Policies;

public class PayrollPolicyDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public Guid WorkCalendarPolicyId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Status { get; set; } = "draft";
	public bool IsDefault { get; set; }
	public string DayCalculationType { get; set; } = "working-days";
	public int? ManualDays { get; set; }
	public List<PayrollPolicyManualMonthDto> ManualMonthDays { get; set; } = [];
	public string SaturdayWorkPolicy { get; set; } = "full-day";
	public List<PayrollPolicyHolidayOverrideDto> HolidayOverrides { get; set; } = [];
	public int Version { get; set; }
	public DateTime CreationTime { get; set; }
	public Guid? CreatorId { get; set; }
	public DateTime? LastModificationTime { get; set; }
	public Guid? LastModifierId { get; set; }
}

public class CreateUpdatePayrollPolicyDto
{
	public Guid AcademicYearId { get; set; }
	public Guid WorkCalendarPolicyId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Status { get; set; } = "draft";
	public bool IsDefault { get; set; }
	public string DayCalculationType { get; set; } = "working-days";
	public int? ManualDays { get; set; }
	public List<PayrollPolicyManualMonthDto> ManualMonthDays { get; set; } = [];
	public string SaturdayWorkPolicy { get; set; } = "full-day";
	public List<PayrollPolicyHolidayOverrideDto> HolidayOverrides { get; set; } = [];
}

public class PayrollPolicyManualMonthDto
{
	public int Year { get; set; }
	public int Month { get; set; }
	public int ManualDays { get; set; }
}

public class PayrollPolicyHolidayOverrideDto
{
	public DateTime Date { get; set; }
	public string HolidayName { get; set; } = string.Empty;
	public bool IncludedInPayroll { get; set; } = true;
	public Guid? CalendarHolidayId { get; set; }
}
