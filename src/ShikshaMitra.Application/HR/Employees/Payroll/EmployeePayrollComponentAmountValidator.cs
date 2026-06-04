using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Payroll;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp;

namespace ShikshaMitra.HR.Employees.Payroll;

internal static class EmployeePayrollComponentAmountValidator
{
	public static void EnsureNoComponentOverlap(
		IEnumerable<EmployeePayrollResolvedLine<EarningComponent>> earningLines,
		IEnumerable<EmployeePayrollResolvedLine<DeductionComponent>> deductionLines)
	{
		var earningIds = earningLines.Where(x => x.Master is not null).Select(x => x.Master!.Id).ToHashSet();
		var overlap = deductionLines
			.Where(x => x.Master is not null)
			.Select(x => x.Master!.Id)
			.Where(earningIds.Contains)
			.ToList();
		if (overlap.Count == 0)
		{
			return;
		}

		throw new BusinessException(HRErrorCodes.EmployeePayrollComponentInBothLists)
			.WithData("overlappingComponentIds", string.Join(",", overlap));
	}
}
