using System;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public static class LearningTaskScheduleTimestamp
{
	public static DateTimeOffset ToSchoolTime(DateTime value) =>
		FromUtcStorage(value).ToOffset(TimeSpan.FromMinutes(330));

	// Calendar dates are school-local date labels; UTC kind keeps their SQL parameters compatible.
	public static DateTime ToSchoolDate(DateTime value) =>
		DateTime.SpecifyKind(ToSchoolTime(value).Date, DateTimeKind.Utc);

	// Schedule values use the UTC clock in storage. An explicit offset avoids applying
	// the host/user timezone to that clock again when serializing legacy DateTime values.
	public static DateTimeOffset FromUtcStorage(DateTime value) =>
		new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

	public static DateTimeOffset? FromUtcStorage(DateTime? value) =>
		value.HasValue ? FromUtcStorage(value.Value) : null;
}
