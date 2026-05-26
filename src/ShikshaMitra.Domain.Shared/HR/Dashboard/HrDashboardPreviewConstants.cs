namespace ShikshaMitra.HR.Dashboard;

public static class HrDashboardKeys
{
	public const string Main = "Main";
	public const string Attendance = "Attendance";
	public const string Payroll = "Payroll";

	public static readonly string[] All = [Main, Attendance, Payroll];
}

public static class HrDashboardPreviewStatus
{
	public const string Empty = "Empty";
	public const string Ready = "Ready";
	public const string Refreshing = "Refreshing";
	public const string Failed = "Failed";
}
