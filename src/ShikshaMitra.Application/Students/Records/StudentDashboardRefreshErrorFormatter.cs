using System;
using Volo.Abp;

namespace ShikshaMitra.Students.Records;

internal static class StudentDashboardRefreshErrorFormatter
{
	public static string Format(Exception exception)
	{
		if (exception is BusinessException businessException)
		{
			if (!string.IsNullOrWhiteSpace(businessException.Code))
			{
				return businessException.Code;
			}

			if (!string.IsNullOrWhiteSpace(businessException.Message) &&
			    !businessException.Message.StartsWith("Exception of type", StringComparison.Ordinal))
			{
				return businessException.Message;
			}
		}

		return "Student dashboard refresh failed.";
	}
}
