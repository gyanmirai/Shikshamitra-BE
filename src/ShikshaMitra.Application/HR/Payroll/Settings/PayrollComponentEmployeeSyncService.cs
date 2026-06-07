using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Payroll.Settings;

public class PayrollComponentEmployeeSyncService(
	IEmployeePayrollComponentRepository employeePayrollComponentRepository,
	IEmployeeCacheManager employeeCacheManager,
	IUnitOfWorkManager unitOfWorkManager,
	PayrollSnapshotReviewInvalidator payrollSnapshotReviewInvalidator) : ITransientDependency
{
	public Task SyncEarningAsync(
		EarningComponent earningComponent,
		PayrollComponentApplyMode applyMode,
		string? effectiveMonth,
		DateTime now) =>
		SyncAsync(
			earningComponent.Id,
			isEarning: true,
			applyMode,
			effectiveMonth,
			now,
			row => row.UpdateComponent(
				earningComponent.Name,
				earningComponent.Description,
				ResolveComponentAmount(
					earningComponent.CalculationType,
					earningComponent.Amount,
					earningComponent.Percentage,
					row.Amount),
				earningComponent.CalculationType == CalculationType.Amount,
				row.IsTemp,
				earningComponent.CalculationType == CalculationType.Amount
					? SalaryComponentType.Fixed
					: SalaryComponentType.Percentage,
				PayrollComponentImpactProjection.MapDependency(earningComponent.DependOn),
				usesMasterValue: true));

	public Task SyncDeductionAsync(
		DeductionComponent deductionComponent,
		PayrollComponentApplyMode applyMode,
		string? effectiveMonth,
		DateTime now) =>
		SyncAsync(
			deductionComponent.Id,
			isEarning: false,
			applyMode,
			effectiveMonth,
			now,
			row => row.UpdateComponent(
				deductionComponent.Name,
				deductionComponent.Description,
				ResolveComponentAmount(
					deductionComponent.CalculationType,
					deductionComponent.Amount,
					deductionComponent.Percentage,
					row.Amount),
				deductionComponent.CalculationType == CalculationType.Amount,
				row.IsTemp,
				deductionComponent.CalculationType == CalculationType.Amount
					? SalaryComponentType.Fixed
					: SalaryComponentType.Percentage,
				PayrollComponentImpactProjection.MapDependency(deductionComponent.DependOn),
				usesMasterValue: true));

	public async Task DeleteLinkedRowsAsync(Guid componentId, bool isEarning)
	{
		var linkedRows = await employeePayrollComponentRepository.GetByComponentIdAsync(componentId, isEarning);
		var linkedEmployeeIds = linkedRows.Select(x => x.EmployeeId).Distinct().ToList();

		await employeePayrollComponentRepository.DeleteByComponentIdAsync(componentId, isEarning);
		await SaveChangesIfActiveAsync();
		await InvalidateEmployeesAsync(linkedEmployeeIds);
	}

	private async Task SyncAsync(
		Guid componentId,
		bool isEarning,
		PayrollComponentApplyMode applyMode,
		string? effectiveMonth,
		DateTime now,
		Action<EmployeePayrollComponent> updateRow)
	{
		var linkedRows = await employeePayrollComponentRepository.GetByComponentIdAsync(componentId, isEarning);

		if (PayrollComponentImpactProjection.IsFutureEffectiveMonth(effectiveMonth, now))
		{
			await MarkFutureSyncedRowsAsync(linkedRows, applyMode);
			return;
		}

		var rowsToSync = linkedRows
			.Where(x => applyMode == PayrollComponentApplyMode.ForceApply || x.UsesMasterValue)
			.ToList();

		if (rowsToSync.Count == 0)
		{
			return;
		}

		foreach (var row in rowsToSync)
		{
			updateRow(row);
			await employeePayrollComponentRepository.UpdateAsync(row, autoSave: false);
		}

		await SaveChangesIfActiveAsync();
		await InvalidateEmployeesAsync(rowsToSync.Select(x => x.EmployeeId).Distinct());
	}

	private async Task MarkFutureSyncedRowsAsync(
		IReadOnlyList<EmployeePayrollComponent> linkedRows,
		PayrollComponentApplyMode applyMode)
	{
		if (applyMode != PayrollComponentApplyMode.ForceApply)
		{
			return;
		}

		var rowsToReset = linkedRows.Where(x => !x.UsesMasterValue).ToList();
		if (rowsToReset.Count == 0)
		{
			return;
		}

		foreach (var row in rowsToReset)
		{
			row.MarkUsesMasterValue();
			await employeePayrollComponentRepository.UpdateAsync(row, autoSave: false);
		}

		await SaveChangesIfActiveAsync();
		await InvalidateEmployeesAsync(rowsToReset.Select(x => x.EmployeeId).Distinct());
	}

	private async Task SaveChangesIfActiveAsync()
	{
		if (unitOfWorkManager.Current != null)
		{
			await unitOfWorkManager.Current.SaveChangesAsync();
		}
	}

	private async Task InvalidateEmployeesAsync(IEnumerable<Guid> employeeIds)
	{
		var ids = employeeIds.Distinct().ToList();
		foreach (var employeeId in ids)
		{
			await employeeCacheManager.InvalidateEmployeeAsync(employeeId);
		}

		await payrollSnapshotReviewInvalidator.InvalidateUnlockedForEmployeesAsync(ids);
	}

	private static decimal ResolveComponentAmount(
		CalculationType calculationType,
		decimal? amount,
		decimal? percentage,
		decimal fallbackAmount) =>
		PayrollComponentImpactProjection.ResolveComponentAmount(
			calculationType,
			amount,
			percentage,
			fallbackAmount);
}
