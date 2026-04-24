using ShikshaMitra.HR;
using System;

namespace ShikshaMitra.Common.Middleware;

internal static class ApiProblemDetailsErrorMessages
{
	internal const string GenericInternalErrorDetail = "An internal error occurred during your request!";

	internal static string NormalizeBusinessDetail(string? detail, string? errorCode, string defaultDetail)
	{
		if (!IsGenericInternalDetail(detail))
		{
			return string.IsNullOrWhiteSpace(detail) ? defaultDetail : detail!;
		}

		if (string.Equals(errorCode, HRErrorCodes.LeaveEmployeeNotFound, StringComparison.Ordinal))
		{
			return "Current user is not linked to an employee profile, so this leave action is not allowed.";
		}

		if (string.Equals(errorCode, HRErrorCodes.LeaveInvalidMonthForAcademicYear, StringComparison.Ordinal))
		{
			return "Selected month is outside the selected academic year range.";
		}

		if (string.Equals(errorCode, HRErrorCodes.LeaveTargetEmployeeRequired, StringComparison.Ordinal))
		{
			return "Target employee is required for this leave action.";
		}

		return defaultDetail;
	}

	internal static bool IsGenericInternalDetail(string? detail)
	{
		return string.Equals(detail?.Trim(), GenericInternalErrorDetail, StringComparison.OrdinalIgnoreCase);
	}
}
