using System;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionEventUtcTime
{
	public static DateTime Normalize(DateTime value) => value.Kind switch
	{
		DateTimeKind.Local => value.ToUniversalTime(),
		_ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
	};

	public static DateTime? Normalize(DateTime? value) => value.HasValue ? Normalize(value.Value) : null;
}
