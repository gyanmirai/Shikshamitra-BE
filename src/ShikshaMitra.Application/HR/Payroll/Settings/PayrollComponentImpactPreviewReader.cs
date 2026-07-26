using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Timing;

namespace ShikshaMitra.HR.Payroll.Settings;

public class PayrollComponentImpactPreviewReader(
	IEmployeePayrollComponentRepository employeePayrollComponentRepository,
	IRepository<Employee, Guid> employeeRepository,
	ISalarySlipRepository salarySlipRepository,
	IAsyncQueryableExecuter asyncExecuter,
	IClock clock) : ITransientDependency
{
	public async Task<PayrollComponentUpdateImpactPreviewDto> BuildUpdatePreviewAsync(
		Guid componentId,
		bool isEarning,
		string? requestedEffectiveMonth,
		Func<string, int, int, int, int, string> buildPreviewHash,
		bool includeEmployees,
		int skipCount,
		int maxResultCount)
	{
		var linkedRows = await employeePayrollComponentRepository.GetByComponentIdAsync(componentId, isEarning);
		var employeeIds = linkedRows.Select(x => x.EmployeeId).Distinct().ToList();
		var effectiveMonth = PayrollComponentImpactProjection.ResolveEffectiveMonth(requestedEffectiveMonth, clock.Now);
		var slips = await salarySlipRepository.GetByMonthAsync(effectiveMonth);
		var linkedSlipRows = slips.Where(x => employeeIds.Contains(x.EmployeeId)).ToList();
		var finalizedSlipCount = linkedSlipRows.Count(
			x => x.Status is SalarySlipStatus.Finalized or SalarySlipStatus.Paid);
		var draftSlipCount = linkedSlipRows.Count(x => x.Status == SalarySlipStatus.Draft);

		return await BuildPreviewAsync(
			linkedRows,
			employeeIds,
			effectiveMonth,
			finalizedSlipCount,
			draftSlipCount,
			buildPreviewHash,
			includeEmployees,
			skipCount,
			maxResultCount);
	}

	public async Task<PayrollComponentUpdateImpactPreviewDto> BuildDeletePreviewAsync(
		Guid componentId,
		bool isEarning,
		Func<IReadOnlyCollection<Guid>, int, int, int, int, string> buildPreviewHash,
		bool includeEmployees,
		int skipCount,
		int maxResultCount)
	{
		var linkedRows = await employeePayrollComponentRepository.GetByComponentIdAsync(componentId, isEarning);
		var employeeIds = linkedRows.Select(x => x.EmployeeId).Distinct().ToList();
		var month = clock.Now.ToString("yyyy-MM");
		var slipCounts = await salarySlipRepository.GetStatusCountsByEmployeeIdsAsync(employeeIds);

		return await BuildPreviewAsync(
			linkedRows,
			employeeIds,
			month,
			slipCounts.FinalizedSlipCount,
			slipCounts.DraftSlipCount,
			(effectiveMonth, syncedCount, overrideCount, finalizedSlipCount, draftSlipCount) =>
				buildPreviewHash(employeeIds, syncedCount, overrideCount, finalizedSlipCount, draftSlipCount),
			includeEmployees,
			skipCount,
			maxResultCount);
	}

	private async Task<PayrollComponentUpdateImpactPreviewDto> BuildPreviewAsync(
		IReadOnlyList<EmployeePayrollComponent> linkedRows,
		IReadOnlyCollection<Guid> employeeIds,
		string effectiveMonth,
		int finalizedSlipCount,
		int draftSlipCount,
		Func<string, int, int, int, int, string> buildPreviewHash,
		bool includeEmployees,
		int skipCount,
		int maxResultCount)
	{
		var employeeNames = await GetEmployeeNamesAsync(employeeIds);
		var syncedRows = linkedRows.Where(x => x.UsesMasterValue).ToList();
		var overrideRows = linkedRows.Where(x => !x.UsesMasterValue).ToList();

		return new PayrollComponentUpdateImpactPreviewDto
		{
			LinkedEmployeeCount = employeeIds.Count,
			SyncedEmployeeCount = syncedRows.Select(x => x.EmployeeId).Distinct().Count(),
			ManualOverrideEmployeeCount = overrideRows.Select(x => x.EmployeeId).Distinct().Count(),
			UnaffectedEmployeeCount = await GetUnaffectedEmployeeCountAsync(employeeIds),
			FinalizedSlipCount = finalizedSlipCount,
			DraftSlipCount = draftSlipCount,
			EffectiveMonth = effectiveMonth,
			PreviewHash = buildPreviewHash(effectiveMonth, syncedRows.Count, overrideRows.Count, finalizedSlipCount, draftSlipCount),
			SyncedEmployees = PayrollComponentImpactProjection.BuildEmployeeSamples(syncedRows, employeeNames, includeEmployees, skipCount, maxResultCount),
			ManualOverrideEmployees = PayrollComponentImpactProjection.BuildEmployeeSamples(overrideRows, employeeNames, includeEmployees, skipCount, maxResultCount)
		};
	}

	private async Task<Dictionary<Guid, string>> GetEmployeeNamesAsync(IReadOnlyCollection<Guid> employeeIds)
	{
		if (employeeIds.Count == 0)
		{
			return new Dictionary<Guid, string>();
		}

		var queryable = await employeeRepository.GetQueryableAsync();
		var employees = await asyncExecuter.ToListAsync(queryable.Where(x => employeeIds.Contains(x.Id)));
		return employees.ToDictionary(
			x => x.Id,
			x => string.IsNullOrWhiteSpace(x.EmployeeName) ? x.EmployeeCode : x.EmployeeName);
	}

	private async Task<int> GetUnaffectedEmployeeCountAsync(IReadOnlyCollection<Guid> linkedEmployeeIds)
	{
		var queryable = await employeeRepository.GetQueryableAsync();
		var totalEmployees = await asyncExecuter.CountAsync(queryable);
		return Math.Max(0, totalEmployees - linkedEmployeeIds.Count);
	}
}
