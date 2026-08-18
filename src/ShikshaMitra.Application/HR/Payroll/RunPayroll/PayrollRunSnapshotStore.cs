using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollRunSnapshotStore(
	IRepository<PayrollRunReview, Guid> payrollRunReviewRepository,
	IRepository<PayrollRunItemSnapshot, Guid> payrollRunItemSnapshotRepository,
	ICurrentTenant currentTenant,
	IGuidGenerator guidGenerator,
	IUnitOfWorkManager unitOfWorkManager,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public async Task<List<PayrollRunItemSnapshot>> GetByMonthAsync(string monthYear)
	{
		var query = await payrollRunItemSnapshotRepository.GetQueryableAsync();

		return await asyncExecuter.ToListAsync(query.Where(x => x.MonthYear == monthYear));
	}

	public async Task UpsertAsync(
		string monthYear,
		IReadOnlyCollection<RunPayrollItem> items,
		bool preserveReviewedSnapshots = false)
	{
		var existingByEmployee = (await GetByMonthAsync(monthYear))
			.ToDictionary(x => x.EmployeeId, x => x);

		foreach (var item in items)
		{
			var issuesJson = JsonSerializer.Serialize(item.IssueCodes ?? new List<string>());
			var attendancePolicyWarningsJson = JsonSerializer.Serialize(item.AttendancePolicyWarnings ?? new List<string>());
			var earningsJson = JsonSerializer.Serialize(item.EarningComponents ?? new List<RunPayrollComponentItem>());
			var deductionsJson = JsonSerializer.Serialize(item.DeductionComponents ?? new List<RunPayrollComponentItem>());
			var employerContributionsJson = JsonSerializer.Serialize(item.EmployerContributions ?? new List<RunPayrollComponentItem>());
			var adjustmentsJson = JsonSerializer.Serialize(item.PayrollAdjustments ?? new List<PayrollAdjustmentDto>());

			if (existingByEmployee.TryGetValue(item.EmployeeId, out var existing))
			{
				if (existing.IsLocked)
				{
					continue;
				}

				var shouldResetReview = !preserveReviewedSnapshots &&
					existing.ReviewStatus == ReviewStatuses.Reviewed &&
					PayrollRunProjection.SnapshotValuesChanged(existing, item, issuesJson, attendancePolicyWarningsJson, earningsJson, deductionsJson, employerContributionsJson, adjustmentsJson);
				PayrollRunProjection.ApplySnapshotValues(existing, item, issuesJson, attendancePolicyWarningsJson, earningsJson, deductionsJson, employerContributionsJson, adjustmentsJson);
				if (shouldResetReview)
				{
					existing.MarkPending();
					await DeleteReviewForEmployeeAsync(monthYear, item.EmployeeId);
				}

				await payrollRunItemSnapshotRepository.UpdateAsync(existing, autoSave: false);
				continue;
			}

			var snapshot = new PayrollRunItemSnapshot(
				guidGenerator.Create(),
				currentTenant.Id,
				monthYear,
				item.EmployeeId);
			PayrollRunProjection.ApplySnapshotValues(snapshot, item, issuesJson, attendancePolicyWarningsJson, earningsJson, deductionsJson, employerContributionsJson, adjustmentsJson);
			await payrollRunItemSnapshotRepository.InsertAsync(snapshot, autoSave: false);
		}

		await unitOfWorkManager.Current!.SaveChangesAsync();
	}

	private async Task DeleteReviewForEmployeeAsync(string monthYear, Guid employeeId)
	{
		var query = await payrollRunReviewRepository.GetQueryableAsync();
		var reviews = await asyncExecuter.ToListAsync(query.Where(x =>
			x.MonthYear == monthYear &&
			x.EmployeeId == employeeId));
		foreach (var review in reviews)
		{
			await payrollRunReviewRepository.DeleteAsync(review, autoSave: false);
		}
	}
}
