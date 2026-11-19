using System;

namespace ShikshaMitra.Students;

/// <summary>A student can have a single legal name; surname is not an identity prerequisite.</summary>
public static class StudentCoreIdentityRules
{
	public static bool IsComplete(string? name, DateTime? dateOfBirth, GenderEnum? gender) =>
		!string.IsNullOrWhiteSpace(name) && dateOfBirth.HasValue && gender.HasValue && Enum.IsDefined(gender.Value);

	public static GenderEnum? ReadGender(string? value) =>
		Enum.TryParse<GenderEnum>(value, true, out var gender) && Enum.IsDefined(gender) ? gender : null;
}
