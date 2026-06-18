using System;

namespace ShikshaMitra.HR.Payroll.Services;

public static class PayrollApplicableMonthNormalizer
{
	public static DateTime? Normalize(DateTime? value) =>
		value.HasValue ? Normalize(value.Value) : null;

	public static DateTime Normalize(DateTime value) =>
		new(value.Year, value.Month, 1, 0, 0, 0, value.Kind);
}
