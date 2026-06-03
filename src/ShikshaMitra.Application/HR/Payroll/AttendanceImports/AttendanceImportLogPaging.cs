using ShikshaMitra.HR;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

public static class AttendanceImportLogPaging
{
	public static void Validate(int skipCount, int maxResultCount, int maxUpperBound)
	{
		if (skipCount < 0)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportInvalidSkipCount);
		}

		if (maxResultCount <= 0 || maxResultCount > maxUpperBound)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportInvalidMaxResultCount)
				.WithData("allowedRange", $"1-{maxUpperBound}")
				.WithData("providedValue", maxResultCount);
		}
	}
}
