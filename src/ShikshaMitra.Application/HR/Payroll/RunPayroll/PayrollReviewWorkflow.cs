using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

[RemoteService(false)]
public class PayrollReviewWorkflow(
	ISalarySlipRepository salarySlipRepository,
	IRepository<PayrollRunReview, Guid> payrollRunReviewRepository,
	IRepository<PayrollRunItemSnapshot, Guid> payrollRunItemSnapshotRepository,
	PayrollAdjustmentReader adjustmentReader,
	PayrollScopedEmployeeResolver scopedEmployeeResolver,
	PayrollRunSnapshotStore snapshotStore,
	PayrollAcademicYearScopeResolver academicYearScopeResolver) : ApplicationService, ITransientDependency
{
	public async Task<PayrollLockResponse> MarkReviewedAsync(PayrollRunReviewRequest request)
	{
		var (year, month, internalMonthYear) = PayrollRunRules.ParseMonth(request.Month);
		await academicYearScopeResolver.ResolveAsync(request.AcademicYearId, year, month);
		var employeeIds = PayrollRunRules.NormalizeReviewEmployeeIds(request.EmployeeIds);
		var actorUserId = CurrentUser.Id ?? throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);

		var targetEmployees = await scopedEmployeeResolver.ResolveScopedTargetEmployeesAsync(year, month, employeeIds);
		await EnsureReviewCanChangeAsync(internalMonthYear, request.Month, targetEmployees);

		var existingByEmployee = await GetReviewsByEmployeeAsync(internalMonthYear);
		var snapshotByEmployee = (await snapshotStore.GetByMonthAsync(internalMonthYear))
			.ToDictionary(x => x.EmployeeId, x => x);
		var missingSnapshotEmployeeIds = targetEmployees
			.Where(e => !snapshotByEmployee.ContainsKey(e.Id))
			.Select(e => e.Id)
			.ToList();
		if (missingSnapshotEmployeeIds.Count > 0)
		{
			throw new BusinessException(HRErrorCodes.PayrollReviewRequiredBeforeLock)
				.WithData("month", request.Month)
				.WithData("employeeIds", missingSnapshotEmployeeIds);
		}

		var reviewedAt = Clock.Now;
		var updated = 0;

		foreach (var employee in targetEmployees)
		{
			if (existingByEmployee.TryGetValue(employee.Id, out var existingReview))
			{
				existingReview.Refresh(actorUserId, reviewedAt);
				await payrollRunReviewRepository.UpdateAsync(existingReview, autoSave: false);
			}
			else
			{
				await payrollRunReviewRepository.InsertAsync(
					new PayrollRunReview(
						GuidGenerator.Create(),
						CurrentTenant.Id,
						internalMonthYear,
						employee.Id,
						actorUserId,
						reviewedAt),
					autoSave: false);
			}

			if (snapshotByEmployee.TryGetValue(employee.Id, out var snapshot) && !snapshot.IsLocked)
			{
				snapshot.MarkReviewed(actorUserId, reviewedAt);
				await payrollRunItemSnapshotRepository.UpdateAsync(snapshot, autoSave: false);
			}
			updated++;
		}

		await UnitOfWorkManager.Current!.SaveChangesAsync();
		return new PayrollLockResponse
		{
			Message = $"Marked {updated} payroll row(s) as reviewed.",
			Month = request.Month,
			EmployeesProcessed = updated
		};
	}

	public async Task<PayrollLockResponse> UnmarkReviewedAsync(PayrollRunReviewRequest request)
	{
		var (year, month, internalMonthYear) = PayrollRunRules.ParseMonth(request.Month);
		await academicYearScopeResolver.ResolveAsync(request.AcademicYearId, year, month);
		var employeeIds = PayrollRunRules.NormalizeReviewEmployeeIds(request.EmployeeIds);
		var targetEmployees = await scopedEmployeeResolver.ResolveScopedTargetEmployeesAsync(year, month, employeeIds);
		await EnsureReviewCanChangeAsync(internalMonthYear, request.Month, targetEmployees);

		var existingByEmployee = await GetReviewsByEmployeeAsync(internalMonthYear);
		var snapshotByEmployee = (await snapshotStore.GetByMonthAsync(internalMonthYear))
			.ToDictionary(x => x.EmployeeId, x => x);
		var adjustmentEmployeeIds = (await adjustmentReader.GetByEmployeeAsync(
				internalMonthYear,
				targetEmployees.Select(x => x.Id).ToList()))
			.Where(x => x.Value.Count > 0)
			.Select(x => x.Key)
			.ToList();
		if (adjustmentEmployeeIds.Count > 0)
		{
			throw new BusinessException(HRErrorCodes.PayrollReviewContainsAdjustments)
				.WithData("month", request.Month)
				.WithData("employeeIds", adjustmentEmployeeIds);
		}

		var removed = 0;
		foreach (var employee in targetEmployees)
		{
			if (!existingByEmployee.TryGetValue(employee.Id, out var existingReview))
			{
				continue;
			}

			await payrollRunReviewRepository.DeleteAsync(existingReview, autoSave: false);
			if (snapshotByEmployee.TryGetValue(employee.Id, out var snapshot))
			{
				snapshot.MarkPending();
				await payrollRunItemSnapshotRepository.UpdateAsync(snapshot, autoSave: false);
			}
			removed++;
		}

		await UnitOfWorkManager.Current!.SaveChangesAsync();
		return new PayrollLockResponse
		{
			Message = $"Removed review from {removed} payroll row(s).",
			Month = request.Month,
			EmployeesProcessed = removed
		};
	}

	internal async Task EnsureReviewCanChangeAsync(
		string internalMonthYear,
		string requestMonth,
		List<PayrollTargetEmployee> targetEmployees)
	{
		var existingSlips = await salarySlipRepository.GetByMonthAsync(internalMonthYear);
		var lockedIds = existingSlips
			.Where(x => x.Status == SalarySlipStatus.Finalized)
			.Select(x => x.EmployeeId)
			.ToHashSet();
		var lockedRequestedIds = targetEmployees
			.Where(e => lockedIds.Contains(e.Id))
			.Select(e => e.Id)
			.ToList();
		if (lockedRequestedIds.Count > 0)
		{
			throw new BusinessException(HRErrorCodes.PayrollReviewCannotChangeLocked)
				.WithData("month", requestMonth)
				.WithData("employeeIds", lockedRequestedIds);
		}
	}

	private async Task<Dictionary<Guid, PayrollRunReview>> GetReviewsByEmployeeAsync(string monthYear)
	{
		var query = await payrollRunReviewRepository.GetQueryableAsync();

		return (await AsyncExecuter.ToListAsync(query.Where(x => x.MonthYear == monthYear)))
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ReviewedAt).First());
	}
}
