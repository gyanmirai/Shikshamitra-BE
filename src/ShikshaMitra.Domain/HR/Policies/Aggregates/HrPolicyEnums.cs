namespace ShikshaMitra.HR.Policies;

public enum HrPolicyStatus
{
	Draft = 0,
	Active = 1,
	Archived = 2
}

public enum WorkCalendarHolidaySyncMode
{
	LinkedWithOverrides = 0,
	Snapshot = 1,
	Independent = 2
}

public enum WorkCalendarHolidaySource
{
	CentralCalendar = 0,
	Manual = 1
}
