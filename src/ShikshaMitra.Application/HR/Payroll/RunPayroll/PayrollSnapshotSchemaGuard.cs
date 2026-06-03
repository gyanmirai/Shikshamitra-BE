using System;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

internal static class PayrollSnapshotSchemaGuard
{
	public static bool IsSnapshotSchemaMissing(Exception ex)
	{
		var current = ex;
		while (current != null)
		{
			var message = current.Message ?? string.Empty;
			if (
				message.Contains("HR_PayrollRunItems", StringComparison.OrdinalIgnoreCase) &&
				(
					message.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
					message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase) ||
					message.Contains("no such table", StringComparison.OrdinalIgnoreCase)
				))
			{
				return true;
			}

			current = current.InnerException!;
		}

		return false;
	}
}
