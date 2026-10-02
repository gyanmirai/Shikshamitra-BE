using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace ShikshaMitra.HR.Payroll.Statutory;

// Invoked inside the evidence writer's transaction, under the tenant payroll fence.
public class EsiContinuityInvalidator(
	IPayrollInputRevisionStore revisions,
	IRepository<PayrollRunItemSnapshot, Guid> snapshots,
	IRepository<SalarySlip, Guid> slips,
	IRepository<PayrollRunReview, Guid> reviews,
	IRepository<PayrollPreviewRun, Guid> runs,
	IRepository<PayrollPreviewEmployee, Guid> results,
	IClock clock) : ITransientDependency
{
	public async Task InvalidateAsync(string sourceMonth, IReadOnlyCollection<Guid> employeeIds)
	{
		if (employeeIds.Count == 0) return;
		await revisions.LockAsync();
		var month = DateTime.ParseExact(sourceMonth, "yyyy-MM", CultureInfo.InvariantCulture);
		var (_, end) = EsiStatutoryCalculator.GetContributionPeriod(month);
		var months = Enumerable.Range(1, 5).Select(month.AddMonths).Where(x => x <= end)
			.Select(x => x.ToString("yyyy-MM", CultureInfo.InvariantCulture)).ToList();
		if (months.Count == 0) return;
		var ids = employeeIds.Distinct().ToList();
		// Finalized payslips supersede locked snapshots, including non-covered evidence.
		var sourceSlips = await slips.GetListAsync(x => x.MonthYear == sourceMonth &&
			Enumerable.Contains(ids, x.EmployeeId) &&
			(x.Status == SalarySlipStatus.Finalized || x.Status == SalarySlipStatus.Paid));
		var sourceSnapshots = await snapshots.GetListAsync(x => x.MonthYear == sourceMonth &&
			Enumerable.Contains(ids, x.EmployeeId) && x.IsLocked);
		ids = ids.Where(id => sourceSlips.Any(x => x.EmployeeId == id && x.EmployeeStateInsuranceRuleId.HasValue) ||
			sourceSnapshots.Any(x => x.EmployeeId == id && x.EmployeeStateInsuranceRuleId.HasValue)).ToList();
		if (ids.Count == 0) return;

		var immutable = (await slips.GetListAsync(x => Enumerable.Contains(months, x.MonthYear) &&
			(x.Status == SalarySlipStatus.Finalized || x.Status == SalarySlipStatus.Paid)))
			.Select(x => (x.EmployeeId, x.MonthYear)).ToHashSet();
		var candidateSnapshots = await snapshots.GetListAsync(x => Enumerable.Contains(months, x.MonthYear));
		immutable.UnionWith(candidateSnapshots.Where(x => x.IsLocked).Select(x => (x.EmployeeId, x.MonthYear)));
		var affectedRuns = await runs.GetListAsync(x => Enumerable.Contains(months, x.Month) &&
			(x.Status == "QUEUED" || x.Status == "RUNNING" || x.Status == "SUCCEEDED"));
		foreach (var run in affectedRuns)
		{
			var affectsRun = run.Status is "QUEUED" or "RUNNING" ||
				(await results.GetListAsync(x => x.RunId == run.Id && Enumerable.Contains(ids, x.EmployeeId)))
				.Any(x => !immutable.Contains((x.EmployeeId, run.Month)));
			if (!affectsRun) continue;
			run.Supersede(clock.Now);
			await runs.UpdateAsync(run);
		}
		foreach (var review in await reviews.GetListAsync(x => Enumerable.Contains(months, x.MonthYear) &&
			Enumerable.Contains(ids, x.EmployeeId)))
			if (!immutable.Contains((review.EmployeeId, review.MonthYear))) await reviews.DeleteAsync(review);
		foreach (var snapshot in candidateSnapshots.Where(x => ids.Contains(x.EmployeeId) &&
			!immutable.Contains((x.EmployeeId, x.MonthYear))))
			await snapshots.DeleteAsync(snapshot);
	}
}