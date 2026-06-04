using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

internal static class SalarySlipMonthYearNormalizer
{
	public static string Normalize(string monthYear)
	{
		if (!PayrollMonthNormalizer.TryNormalizeToInternal(monthYear, out var normalizedMonthYear))
		{
			throw new BusinessException(string.IsNullOrWhiteSpace(monthYear)
				? HRErrorCodes.MonthYearRequired
				: HRErrorCodes.InvalidMonthYearFormat);
		}

		return normalizedMonthYear;
	}
}
