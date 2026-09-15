using System;

namespace ShikshaMitra.Academic.Examination.Exams;

internal static class ExamScheduleStatusResolver
{
	private const string InstitutionTimeZoneId = "Asia/Kolkata";
	private const string WindowsInstitutionTimeZoneId = "India Standard Time";
	private static readonly TimeZoneInfo InstitutionTimeZone = GetInstitutionTimeZone();

	public static ExamStatus Resolve(
		ExamStatus persistedStatus,
		DateTime? scheduledStart,
		DateTime? scheduledEnd,
		DateTime utcNow)
	{
		if (persistedStatus == ExamStatus.Pending)
		{
			return ExamStatus.Pending;
		}

		if (!scheduledStart.HasValue || !scheduledEnd.HasValue)
		{
			return ExamStatus.Pending;
		}

		var institutionNow = TimeZoneInfo.ConvertTimeFromUtc(NormalizeUtc(utcNow), InstitutionTimeZone);
		if (institutionNow >= scheduledStart.Value && institutionNow <= scheduledEnd.Value)
		{
			return ExamStatus.Ongoing;
		}

		return institutionNow > scheduledEnd.Value ? ExamStatus.Completed : persistedStatus;
	}

	private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
	{
		DateTimeKind.Utc => value,
		DateTimeKind.Local => value.ToUniversalTime(),
		_ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
	};

	private static TimeZoneInfo GetInstitutionTimeZone()
	{
		try
		{
			return TimeZoneInfo.FindSystemTimeZoneById(InstitutionTimeZoneId);
		}
		catch (TimeZoneNotFoundException)
		{
			return TimeZoneInfo.FindSystemTimeZoneById(WindowsInstitutionTimeZoneId);
		}
	}
}
