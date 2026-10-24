using System;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public static class LearningTaskScheduleTimestamp
{
	// Schedule values use the UTC clock in storage. An explicit offset avoids applying
	// the host/user timezone to that clock again when serializing legacy DateTime values.
	public static DateTimeOffset FromUtcStorage(DateTime value) =>
		new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

	public static DateTimeOffset? FromUtcStorage(DateTime? value) =>
		value.HasValue ? FromUtcStorage(value.Value) : null;
}
