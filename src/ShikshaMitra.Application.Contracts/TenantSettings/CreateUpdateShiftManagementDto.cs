using System;

namespace ShikshaMitra.TenantSettings;

public class CreateUpdateShiftManagementDto
{
	public string ShiftName { get; set; }
	public TimeOnly StartTime { get; set; }
	public TimeOnly EndTime { get; set; }
}
