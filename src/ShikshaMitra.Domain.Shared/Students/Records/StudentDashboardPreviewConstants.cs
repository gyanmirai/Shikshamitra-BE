namespace ShikshaMitra.Students.Records;

public static class StudentDashboardKeys
{
	public const string Reporting = "Reporting";
	public static readonly string[] All = [Reporting];
}

public static class StudentDashboardPreviewStatus
{
	public const string Empty = "Empty";
	public const string Ready = "Ready";
	public const string Refreshing = "Refreshing";
	public const string Failed = "Failed";
}

public static class StudentDashboardScopeKeys
{
	public const string StudentWide = "student-wide";
}

public static class StudentDashboardErrorCodes
{
	public const string InvalidKey = "StudentDashboard:InvalidKey";
	public const string ScopeChanged = "StudentDashboard:ScopeChanged";
}
