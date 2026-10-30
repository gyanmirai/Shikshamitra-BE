using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Academic.Examination.Exams;

internal static class ExamScheduleStatusResolver
{
	private const string InstitutionTimeZoneId = "Asia/Kolkata";
	private const string WindowsInstitutionTimeZoneId = "India Standard Time";
	private static readonly TimeZoneInfo InstitutionTimeZone = GetInstitutionTimeZone();

	// Stored ISO offsets are metadata: date + clock components describe the
	// institution's civil time, rather than absolute UTC instants.
	public static ExamScheduleWindow GetWindow(IEnumerable<(DateTime? ExamDate, DateTime? StartTime, DateTime? EndTime)> subjects)
	{
		var scheduled = subjects.Where(subject => subject.ExamDate.HasValue).ToList();
		if (scheduled.Count == 0) return new(null, null, null);
		return new(
			scheduled.Min(subject => subject.ExamDate!.Value.Date),
			scheduled.Min(subject => subject.ExamDate!.Value.Date.Add(subject.StartTime?.TimeOfDay ?? TimeSpan.Zero)),
			scheduled.Max(subject => subject.EndTime.HasValue
				? subject.ExamDate!.Value.Date.Add(subject.EndTime.Value.TimeOfDay)
				: subject.ExamDate!.Value.Date.AddDays(1).AddTicks(-1)));
	}

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

		var institutionNow = GetInstitutionNow(utcNow);
		if (institutionNow >= scheduledStart.Value && institutionNow <= scheduledEnd.Value)
		{
			return ExamStatus.Ongoing;
		}

		return institutionNow > scheduledEnd.Value ? ExamStatus.Completed : persistedStatus;
	}

	public static DateTime GetInstitutionNow(DateTime utcNow) =>
		TimeZoneInfo.ConvertTimeFromUtc(NormalizeUtc(utcNow), InstitutionTimeZone);

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

internal sealed record ExamScheduleWindow(DateTime? FirstDate, DateTime? StartAt, DateTime? EndAt);
