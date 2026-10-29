using System;

namespace ShikshaMitra.Common.Middleware;

internal static class ApiProblemDetailsErrorMessages
{
	internal const string GenericInternalErrorDetail = "An internal error occurred during your request!";
	internal const string GenericBusinessRuleDetail = "Business rule violated.";

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

		if (IsGenericBusinessRuleDetail(detail))
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

	internal static bool IsGenericBusinessRuleDetail(string? detail)
	{
		return string.Equals(detail?.Trim(), GenericBusinessRuleDetail, StringComparison.OrdinalIgnoreCase) ||
		       string.Equals(detail?.Trim(), "Business rule violated", StringComparison.OrdinalIgnoreCase) ||
		       string.Equals(detail?.Trim(), "One or more business rules were violated.", StringComparison.OrdinalIgnoreCase);
	}
}
