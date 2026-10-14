using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollSnapshotReviewInvalidator(
	IRepository<PayrollRunReview, Guid> payrollRunReviewRepository,
	IRepository<PayrollRunItemSnapshot, Guid> payrollRunItemSnapshotRepository,
	IAsyncQueryableExecuter asyncExecuter,
	IUnitOfWorkManager unitOfWorkManager, IRepository<ShikshaMitra.HR.Payroll.SalarySlips.SalarySlip, Guid> slips) : ITransientDependency
{
	public async Task InvalidateUnlockedForEmployeesAsync(IEnumerable<Guid> employeeIds)
	{
		var employeeIdSet = employeeIds.Where(id => id != Guid.Empty).Distinct().ToHashSet();
		if (employeeIdSet.Count == 0)
		{
			return;
		}

		var snapshotQuery = await payrollRunItemSnapshotRepository.GetQueryableAsync();
		var issued = await slips.GetQueryableAsync();
		var snapshots = await asyncExecuter.ToListAsync(snapshotQuery.Where(x =>
			employeeIdSet.Contains(x.EmployeeId) &&
			!x.IsLocked && !issued.Any(slip => slip.EmployeeId == x.EmployeeId && slip.MonthYear == x.MonthYear &&
			(slip.Status == ShikshaMitra.HR.Payroll.SalarySlips.SalarySlipStatus.Finalized || slip.Status == ShikshaMitra.HR.Payroll.SalarySlips.SalarySlipStatus.Paid))));
		if (snapshots.Count == 0)
		{
			return;
		}

		var affectedKeys = snapshots
			.Select(x => new { x.MonthYear, x.EmployeeId })
			.Distinct()
			.ToList();
		var affectedMonths = affectedKeys.Select(x => x.MonthYear).Distinct().ToHashSet();

		var reviewQuery = await payrollRunReviewRepository.GetQueryableAsync();
		var candidateReviews = await asyncExecuter.ToListAsync(reviewQuery.Where(x =>
			affectedMonths.Contains(x.MonthYear) &&
			employeeIdSet.Contains(x.EmployeeId)));
		foreach (var review in candidateReviews.Where(review =>
			         affectedKeys.Any(key => key.MonthYear == review.MonthYear && key.EmployeeId == review.EmployeeId)))
		{
			await payrollRunReviewRepository.DeleteAsync(review, autoSave: false);
		}

		foreach (var snapshot in snapshots)
		{
			await payrollRunItemSnapshotRepository.DeleteAsync(snapshot, autoSave: false);
		}

		if (unitOfWorkManager.Current != null)
		{
			await unitOfWorkManager.Current.SaveChangesAsync();
		}
	}
}
