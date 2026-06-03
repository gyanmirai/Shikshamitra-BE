using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Employees;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollIneligibleRowCleaner(
	IRepository<PayrollRunReview, Guid> payrollRunReviewRepository,
	IRepository<PayrollRunItemSnapshot, Guid> payrollRunItemSnapshotRepository,
	IRepository<PayrollAdjustment, Guid> payrollAdjustmentRepository,
	IUnitOfWorkManager unitOfWorkManager,
	IAsyncQueryableExecuter asyncExecuter,
	ILogger<PayrollIneligibleRowCleaner> logger)
	: ITransientDependency
{
	public async Task CleanupAsync(
		string monthYear,
		IReadOnlyCollection<Employee> scopedActiveEmployees,
		IReadOnlyCollection<Employee> eligibleEmployees)
	{
		var ineligibleEmployeeIds = scopedActiveEmployees
			.Select(e => e.Id)
			.Except(eligibleEmployees.Select(e => e.Id))
			.ToHashSet();
		if (ineligibleEmployeeIds.Count == 0)
		{
			return;
		}

		var snapshotQuery = await payrollRunItemSnapshotRepository.GetQueryableAsync();

		var snapshots = await asyncExecuter.ToListAsync(snapshotQuery.Where(x =>
			x.MonthYear == monthYear &&
			ineligibleEmployeeIds.Contains(x.EmployeeId)));
		var lockedSnapshotEmployeeIds = snapshots
			.Where(x => x.IsLocked)
			.Select(x => x.EmployeeId)
			.ToHashSet();
		var cleanupEmployeeIds = ineligibleEmployeeIds
			.Except(lockedSnapshotEmployeeIds)
			.ToHashSet();
		if (cleanupEmployeeIds.Count == 0)
		{
			logger.LogInformation(
				"Skipped payroll ineligible-row cleanup for {MonthYear}: {IneligibleEmployeeCount} ineligible employees all have locked snapshots.",
				monthYear,
				ineligibleEmployeeIds.Count);
			return;
		}

		var deletedSnapshotCount = 0;
		foreach (var snapshot in snapshots.Where(x => cleanupEmployeeIds.Contains(x.EmployeeId)))
		{
			await payrollRunItemSnapshotRepository.DeleteAsync(snapshot, autoSave: false);
			deletedSnapshotCount++;
		}

		var reviewQuery = await payrollRunReviewRepository.GetQueryableAsync();
		var reviews = await asyncExecuter.ToListAsync(reviewQuery.Where(x =>
			x.MonthYear == monthYear &&
			cleanupEmployeeIds.Contains(x.EmployeeId)));
		var deletedReviewCount = 0;
		foreach (var review in reviews)
		{
			await payrollRunReviewRepository.DeleteAsync(review, autoSave: false);
			deletedReviewCount++;
		}

		var adjustmentQuery = await payrollAdjustmentRepository.GetQueryableAsync();
		var adjustments = await asyncExecuter.ToListAsync(adjustmentQuery.Where(x =>
			x.MonthYear == monthYear &&
			cleanupEmployeeIds.Contains(x.EmployeeId)));
		var deletedAdjustmentCount = 0;
		foreach (var adjustment in adjustments)
		{
			await payrollAdjustmentRepository.DeleteAsync(adjustment, autoSave: false);
			deletedAdjustmentCount++;
		}

		await unitOfWorkManager.Current!.SaveChangesAsync();
		logger.LogInformation(
			"Cleaned payroll rows for {MonthYear}: {IneligibleEmployeeCount} ineligible employees, {DeletedSnapshotCount} snapshots, {DeletedReviewCount} reviews, {DeletedAdjustmentCount} adjustments deleted.",
			monthYear,
			ineligibleEmployeeIds.Count,
			deletedSnapshotCount,
			deletedReviewCount,
			deletedAdjustmentCount);
	}
}
