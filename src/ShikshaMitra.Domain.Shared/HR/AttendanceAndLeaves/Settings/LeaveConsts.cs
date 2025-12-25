namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// Constants for LeaveType entity
/// </summary>
public static class LeaveTypeConsts
{
	public const int MaxTypeLength = 100;
	public const int MaxColorLength = 7; // #RRGGBB
	public const int MaxDescriptionLength = 500;
}

/// <summary>
/// Constants for LeavePolicy entity
/// </summary>
public static class LeavePolicyConsts
{
	public const int MaxPolicyNameLength = 200;
	public const int MaxLeaveTypeLength = 100;
	public const int MaxDescriptionLength = 500;
	public const int MinMaximumDaysPerYear = 1;
	public const int MaxMaximumDaysPerYear = 365;
}
