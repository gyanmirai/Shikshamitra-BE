using System;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public enum EmployeeLeaveStatusType
{
	Pending = 0,
	Approved = 1,
	Rejected = 2,
	Reviewed = 3,
	Cancelled = 4
}

public static class LeaveDurationCalculator
{
	public static int CountInclusiveBusinessDays(DateTime startDate, DateTime endDate)
	{
		var start = startDate.Date;
		var end = endDate.Date;
		if (start > end)
		{
			return 0;
		}

		var count = 0;
		for (var d = start; d <= end; d = d.AddDays(1))
		{
			if (d.DayOfWeek is not DayOfWeek.Saturday && d.DayOfWeek is not DayOfWeek.Sunday)
			{
				count++;
			}
		}

		return count;
	}

	public static int CountLeaveDays(DateTime startDate, DateTime endDate, bool halfDay)
	{
		var businessDays = CountInclusiveBusinessDays(startDate, endDate);
		return halfDay
			? (int)Math.Ceiling(businessDays / 2.0)
			: businessDays;
	}
}