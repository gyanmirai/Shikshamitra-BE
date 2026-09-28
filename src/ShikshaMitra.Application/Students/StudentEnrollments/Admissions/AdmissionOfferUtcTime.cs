using System;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

internal static class AdmissionOfferUtcTime
{
	// API timestamps without a kind represent UTC; treating them as server-local shifts offer expiry.
	internal static DateTime Normalize(DateTime value) => value.Kind switch
	{
		DateTimeKind.Utc => value,
		DateTimeKind.Local => value.ToUniversalTime(),
		_ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
	};
}
