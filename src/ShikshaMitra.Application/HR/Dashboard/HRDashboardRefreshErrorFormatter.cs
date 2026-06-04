using System;
using Volo.Abp;

namespace ShikshaMitra.HR.Dashboard;

internal static class HRDashboardRefreshErrorFormatter
{
	public static string Format(Exception exception)
	{
		if (exception is BusinessException businessException)
		{
			var code = string.IsNullOrWhiteSpace(businessException.Code)
				? null
				: businessException.Code;
			var message = string.IsNullOrWhiteSpace(businessException.Message) ||
				businessException.Message.StartsWith("Exception of type", StringComparison.Ordinal)
					? null
					: businessException.Message;

			if (code != null && message != null)
			{
				return $"{code}: {message}";
			}

			if (code != null)
			{
				return code;
			}

			if (businessException.InnerException?.Message is { Length: > 0 } innerMessage)
			{
				return innerMessage;
			}
		}

		return string.IsNullOrWhiteSpace(exception.Message)
			? "Dashboard refresh failed."
			: exception.Message;
	}
}
