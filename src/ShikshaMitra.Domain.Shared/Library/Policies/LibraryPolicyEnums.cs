namespace ShikshaMitra.Library.Policies;

public enum LibraryBorrowingPolicyState { Draft = 0, Published = 1, Archived = 2 }
public enum LibraryDayCountingMode { CalendarDays = 0, OpenDays = 1 }
public enum LibraryDueDateMode { Manual = 0, Automatic = 1 }

public static class LibraryPolicySettingNames
{
	public const string CalendarTimeZone = "ShikshaMitra.Library.Calendar.TimeZone";
	public const string LegacyHrTimeZone = "ShikshaMitra.HR.Reporting.TimeZone";
}
