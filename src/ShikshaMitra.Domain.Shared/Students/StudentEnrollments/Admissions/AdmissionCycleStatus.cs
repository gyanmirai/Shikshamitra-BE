using System;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public enum AdmissionCycleStatus
{
	Draft = 0,
	Published = 1,
	Closed = 2,
	Archived = 3
}

public enum AdmissionCycleWindowStatus
{
	Upcoming = 0,
	Open = 1,
	Ended = 2,
	Closed = 3,
	Archived = 4
}

public enum AdmissionCycleHistoryAction
{
	Created = 0,
	Updated = 1,
	Published = 2,
	Closed = 3,
	Reopened = 4,
	Archived = 5,
	ScheduleChanged = 6,
	Cloned = 7
}

public static class AdmissionCycleWindowEvaluator
{
	public const string InstitutionTimeZoneId = "Asia/Kolkata";

	public static AdmissionCycleWindowStatus Evaluate(AdmissionCycleStatus status, DateTime startDate,
		DateTime endDate, DateTime utcNow)
		=> EvaluateForDate(status, startDate, endDate, GetInstitutionDate(utcNow));

	public static AdmissionCycleWindowStatus EvaluateForDate(AdmissionCycleStatus status, DateTime startDate,
		DateTime endDate, DateTime institutionDate)
	{
		if (status == AdmissionCycleStatus.Archived) return AdmissionCycleWindowStatus.Archived;
		if (status == AdmissionCycleStatus.Closed) return AdmissionCycleWindowStatus.Closed;

		var today = institutionDate.Date;
		if (today < startDate.Date) return AdmissionCycleWindowStatus.Upcoming;
		return today > endDate.Date ? AdmissionCycleWindowStatus.Ended : AdmissionCycleWindowStatus.Open;
	}

	public static DateTime GetInstitutionDate(DateTime utcNow)
	{
		var normalizedUtc = utcNow.Kind == DateTimeKind.Utc ? utcNow : utcNow.ToUniversalTime();
		try
		{
			return TimeZoneInfo.ConvertTimeFromUtc(normalizedUtc,
				TimeZoneInfo.FindSystemTimeZoneById(InstitutionTimeZoneId)).Date;
		}
		catch (TimeZoneNotFoundException)
		{
			return TimeZoneInfo.ConvertTimeFromUtc(normalizedUtc,
				TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")).Date;
		}
	}
}
