using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Payroll.Statutory;

public class EsiConfigurationPeriodGuard(IRepository<PayrollRunItemSnapshot, Guid> snapshots,
	IRepository<SalarySlip, Guid> slips, IRepository<PayrollRunReview, Guid> reviews,
	IAsyncQueryableExecuter executor) : ITransientDependency
{
	public async Task InvalidateAsync(DateTime from, DateTime? until, IReadOnlyCollection<Guid>? affectedEmployeeIds = null)
	{
		var firstMonth = from.ToString("yyyy-MM", CultureInfo.InvariantCulture);
		var candidates = (await executor.ToListAsync((await snapshots.GetQueryableAsync())
			.Where(x => !x.IsLocked && string.Compare(x.MonthYear, firstMonth) >= 0)))
			.Where(x => Includes(x.MonthYear, from, until) && (affectedEmployeeIds == null || affectedEmployeeIds.Contains(x.EmployeeId))).ToList();
		var finalized = await executor.ToListAsync((await slips.GetQueryableAsync())
			.Where(x => string.Compare(x.MonthYear, firstMonth) >= 0 &&
				(x.Status == SalarySlipStatus.Finalized || x.Status == SalarySlipStatus.Paid)));
		foreach (var month in candidates.GroupBy(x => x.MonthYear))
		{
			var protectedIds = finalized.Where(x => x.MonthYear == month.Key).Select(x => x.EmployeeId).ToHashSet();
			var employeeIds = month.Where(x => !protectedIds.Contains(x.EmployeeId)).Select(x => x.EmployeeId).ToList();
			if (employeeIds.Count == 0) continue;
			await reviews.DeleteAsync(x => x.MonthYear == month.Key && Enumerable.Contains(employeeIds, x.EmployeeId));
			await snapshots.DeleteAsync(x => !x.IsLocked && x.MonthYear == month.Key && Enumerable.Contains(employeeIds, x.EmployeeId));
		}
	}

	private static bool Includes(string month, DateTime from, DateTime? until)
	{
		var date = DateTime.ParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture);
		return date >= from && (!until.HasValue || date < until);
	}
}
