using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollPreviewWorkflow(
	ISalarySlipRepository salarySlipRepository,
	PayrollIneligibleRowCleaner ineligibleRowCleaner,
	PayrollScopedEmployeeResolver scopedEmployeeResolver,
	PayrollRunSnapshotStore snapshotStore,
	PayrollSnapshotRefresher snapshotRefresher,
	PayrollSnapshotInvalidationChecker snapshotInvalidationChecker,
	PayrollAcademicYearScopeResolver academicYearScopeResolver,
	PayrollSnapshotLockProvider snapshotLockProvider,
	PayrollPolicySetupFallbackBuilder policySetupFallbackBuilder,
	ILogger<PayrollPreviewWorkflow> logger) : ITransientDependency
{
	public async Task<RunPayrollResponse> GetPreviewAsync(GetRunPayrollInput input)
	{
		if (input.PageSize <= 0 || input.PageSize > 100)
		{
			throw new BusinessException(HRErrorCodes.PayrollInvalidPageSize)
				.WithData("pageSize", input.PageSize)
				.WithData("maxPageSize", 100);
		}

		try
		{
			var (year, month, internalMonthYear) = PayrollRunRules.ParseMonth(input.Month);
			var academicYearId = await academicYearScopeResolver.ResolveAsync(input.AcademicYearId, year, month);

			logger.LogInformation(
				"Generating payroll preview for {Month} (Year: {Year}, Month: {MonthNum}, Page: {Page}, PageSize: {PageSize})",
				input.Month, year, month, input.Page, input.PageSize);

			return await WithPayrollSnapshotLockAsync(internalMonthYear, async () =>
			{
				var preview = await BuildPreviewRowsAsync(
					internalMonthYear,
					year,
					month,
					academicYearId,
					forceRefresh: input.ForceRefresh);

				var totalCount = preview.Snapshots.Count;
				var items = preview.Snapshots
					.Skip(input.Page * input.PageSize)
					.Take(input.PageSize)
					.Select(PayrollRunProjection.ToItem)
					.ToList();
				var readinessItems = preview.Snapshots.Select(PayrollRunProjection.ToItem).ToList();

				return new RunPayrollResponse
				{
					IsGenerated = preview.IsGenerated,
					TotalCount = totalCount,
					CurrentPage = input.Page,
					PageSize = input.PageSize,
					TotalPages = (int)Math.Ceiling((double)totalCount / input.PageSize),
					Items = items,
					Readiness = PayrollReadinessProjector.BuildSummary(internalMonthYear, readinessItems, preview.IsGenerated)
				};
			});
		}
		catch (BusinessException ex) when (PayrollRunRules.IsPayrollPolicySetupException(ex))
		{
			logger.LogWarning(
				ex,
				"Payroll preview setup issue escaped snapshot refresh for month {Month}. Returning policy setup fallback response. ErrorCode={ErrorCode}",
				input.Month,
				ex.Code);
			return await policySetupFallbackBuilder.BuildPreviewAsync(input);
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex) when (PayrollSnapshotSchemaGuard.IsSnapshotSchemaMissing(ex))
		{
			logger.LogError(ex, "Payroll snapshot schema missing for month {Month}. Migration required.", input.Month);
			throw new BusinessException(HRErrorCodes.PayrollSnapshotMigrationRequired)
				.WithData("month", input.Month)
				.WithData("requiredTable", "HR_PayrollRunItems")
				.WithData("requiredMigration", "20260428034756_PayrollRunItemSnapshots");
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Unexpected error generating payroll preview for month {Month}", input.Month);
			throw new BusinessException(HRErrorCodes.PayrollPreviewFailed)
				.WithData("month", input.Month);
		}
	}

	public async Task<RunPayrollReadinessSummary> GetReadinessAsync(string month, Guid? academicYearId = null)
	{
		try
		{
			var (year, monthNumber, internalMonthYear) = PayrollRunRules.ParseMonth(month);
			var resolvedAcademicYearId = await academicYearScopeResolver.ResolveAsync(academicYearId, year, monthNumber);
			return await WithPayrollSnapshotLockAsync(internalMonthYear, async () =>
			{
				var preview = await BuildPreviewRowsAsync(
					internalMonthYear,
					year,
					monthNumber,
					resolvedAcademicYearId,
					forceRefresh: false);
				var items = preview.Snapshots.Select(PayrollRunProjection.ToItem).ToList();
				var existingSlips = await salarySlipRepository.GetByMonthAsync(internalMonthYear);
				var lockedCount = existingSlips.Count(x =>
					(x.Status is SalarySlipStatus.Finalized or SalarySlipStatus.Paid) &&
					preview.ScopedEmployeeIds.Contains(x.EmployeeId));

				return PayrollReadinessProjector.BuildSummary(
					internalMonthYear,
					items,
					isGenerated: lockedCount > 0,
					employeeCountOverride: preview.ScopedEmployeeIds.Count,
					lockedCountOverride: lockedCount);
			});
		}
		catch (BusinessException ex) when (PayrollRunRules.IsPayrollPolicySetupException(ex))
		{
			logger.LogWarning(
				ex,
				"Payroll readiness setup issue escaped snapshot refresh for month {Month}. Returning policy setup fallback response. ErrorCode={ErrorCode}",
				month,
				ex.Code);
			return await policySetupFallbackBuilder.BuildReadinessAsync(month, academicYearId);
		}
	}

	private async Task<PayrollPreviewRows> BuildPreviewRowsAsync(
		string internalMonthYear,
		int year,
		int month,
		Guid? academicYearId,
		bool forceRefresh)
	{
		var allScopedEmployees = await scopedEmployeeResolver.GetScopedEmployeesAsync(includeUnknownStageEmployees: false);
		var scopedEmployees = await scopedEmployeeResolver.GetScopedPayrollEmployeesAsync(year, month, includeUnknownStageEmployees: false);
		await ineligibleRowCleaner.CleanupAsync(internalMonthYear, allScopedEmployees, scopedEmployees);
		var scopedEmployeeIds = scopedEmployees.Select(e => e.Id).ToHashSet();

		var existingSlips = await salarySlipRepository.GetByMonthAsync(internalMonthYear);
		var finalizedEmployeeIds = existingSlips
			.Where(x => x.Status is SalarySlipStatus.Finalized or SalarySlipStatus.Paid)
			.Select(x => x.EmployeeId)
			.ToHashSet();

		var persistedSnapshots = await snapshotStore.GetByMonthAsync(internalMonthYear);
		var inScopeSnapshots = persistedSnapshots
			.Where(x => scopedEmployeeIds.Contains(x.EmployeeId))
			.OrderBy(x => x.EmployeeName)
			.ToList();
		if (forceRefresh ||
		    inScopeSnapshots.Count < scopedEmployees.Count ||
		    await snapshotInvalidationChecker.HasInvalidationsAsync(internalMonthYear, year, month, academicYearId, scopedEmployeeIds, inScopeSnapshots))
		{
			await snapshotRefresher.RefreshAsync(internalMonthYear, year, month, academicYearId, scopedEmployees, preserveReviewedSnapshots: false);
			persistedSnapshots = await snapshotStore.GetByMonthAsync(internalMonthYear);
			inScopeSnapshots = persistedSnapshots
				.Where(x => scopedEmployeeIds.Contains(x.EmployeeId))
				.OrderBy(x => x.EmployeeName)
				.ToList();
		}

		return new PayrollPreviewRows(inScopeSnapshots, scopedEmployeeIds, finalizedEmployeeIds.Count > 0);
	}

	private Task<T> WithPayrollSnapshotLockAsync<T>(string monthYear, Func<Task<T>> action) =>
		snapshotLockProvider.WithMonthLockAsync(monthYear, action);

	private sealed record PayrollPreviewRows(
		List<PayrollRunItemSnapshot> Snapshots,
		HashSet<Guid> ScopedEmployeeIds,
		bool IsGenerated);
}
