using System;

namespace ShikshaMitra.TenantSettings;

public class CreateUpdateShiftManagementDto
{
	public string ShiftName { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.
	public TimeOnly StartTime { get; set; }
	public TimeOnly EndTime { get; set; }
}
