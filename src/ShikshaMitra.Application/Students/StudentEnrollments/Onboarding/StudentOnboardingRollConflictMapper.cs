using System;
using System.Data.Common;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

/// <summary>Keep provider-specific serialization evidence out of the public command response.</summary>
internal static class StudentOnboardingRollConflictMapper
{
	public static bool IsSerializationConflict(Exception exception)
	{
		for (Exception? current = exception; current != null; current = current.InnerException)
			if (current is DbException database && database.SqlState == "40001") return true;
		return false;
	}
}
