using System;

namespace ShikshaMitra.Common.Middleware;

internal static class ApiProblemDetailsErrorMessages
{
	internal const string GenericInternalErrorDetail = "An internal error occurred during your request!";

	internal static bool ShouldPreferCodeMessage(string? detail)
	{
		if (string.IsNullOrWhiteSpace(detail))
		{
			return true;
		}

		if (IsGenericInternalDetail(detail))
		{
			return true;
		}

		var trimmed = detail.Trim();
		return trimmed.StartsWith("BusinessException:", StringComparison.OrdinalIgnoreCase) ||
		       trimmed.Contains("STACK TRACE:", StringComparison.OrdinalIgnoreCase) ||
		       trimmed.StartsWith("Exception of type '", StringComparison.OrdinalIgnoreCase);
	}

	internal static bool IsGenericInternalDetail(string? detail)
	{
		return string.Equals(detail?.Trim(), GenericInternalErrorDetail, StringComparison.OrdinalIgnoreCase);
	}
}
