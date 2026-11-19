using System;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionSettingsCode
{
	// Use the complete record ID so creation needs no counter or extra database round trip.
	public static string ForCreate(string? suppliedCode, string prefix, Guid id) =>
		string.IsNullOrWhiteSpace(suppliedCode)
			? $"{prefix}_{id:N}".ToUpperInvariant()
			: suppliedCode.Trim().ToUpperInvariant();

	public static string ForUpdate(string? suppliedCode, string existingCode) =>
		string.IsNullOrWhiteSpace(suppliedCode) ? existingCode : suppliedCode.Trim().ToUpperInvariant();
}
