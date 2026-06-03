using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollPolicySetupFallbackBuilder(
	ISalarySlipRepository salarySlipRepository,
	PayrollIneligibleRowCleaner ineligibleRowCleaner,
	PayrollScopedEmployeeResolver scopedEmployeeResolver,
	PayrollRunSnapshotStore snapshotStore,
	PayrollAcademicYearScopeResolver academicYearScopeResolver) : ITransientDependency
{
	public async Task<RunPayrollResponse> BuildPreviewAsync(GetRunPayrollInput input)
	{
		var (year, month, internalMonthYear) = PayrollRunRules.ParseMonth(input.Month);
		await academicYearScopeResolver.ResolveAsync(input.AcademicYearId, year, month);
		var fallback = await BuildItemsAsync(internalMonthYear, year, month);
		var items = fallback.Items
			.Skip(input.Page * input.PageSize)
			.Take(input.PageSize)
			.ToList();

		return new RunPayrollResponse
		{
			IsGenerated = fallback.IsGenerated,
			TotalCount = fallback.Items.Count,
			CurrentPage = input.Page,
			PageSize = input.PageSize,
			TotalPages = input.PageSize <= 0 ? 0 : (int)Math.Ceiling((double)fallback.Items.Count / input.PageSize),
			Items = items,
			Readiness = PayrollReadinessProjector.BuildSummary(
				internalMonthYear,
				fallback.Items,
				fallback.IsGenerated,
				employeeCountOverride: fallback.EmployeeCount,
				lockedCountOverride: fallback.LockedCount)
		};
	}

	public async Task<RunPayrollReadinessSummary> BuildReadinessAsync(string month, Guid? academicYearId = null)
	{
		var (year, monthNumber, internalMonthYear) = PayrollRunRules.ParseMonth(month);
		await academicYearScopeResolver.ResolveAsync(academicYearId, year, monthNumber);
		var fallback = await BuildItemsAsync(internalMonthYear, year, monthNumber);
		return PayrollReadinessProjector.BuildSummary(
			internalMonthYear,
			fallback.Items,
			fallback.IsGenerated,
			employeeCountOverride: fallback.EmployeeCount,
			lockedCountOverride: fallback.LockedCount);
	}

	private async Task<PolicySetupFallbackItems> BuildItemsAsync(
		string internalMonthYear,
		int year,
		int month)
	{
		var allScopedEmployees = await scopedEmployeeResolver.GetScopedEmployeesAsync(includeUnknownStageEmployees: false);
		var scopedEmployees = await scopedEmployeeResolver.GetScopedPayrollEmployeesAsync(year, month, includeUnknownStageEmployees: false);
		await ineligibleRowCleaner.CleanupAsync(internalMonthYear, allScopedEmployees, scopedEmployees);
		var scopedEmployeeIds = scopedEmployees.Select(e => e.Id).ToHashSet();
		var snapshots = (await snapshotStore.GetByMonthAsync(internalMonthYear))
			.Where(x => scopedEmployeeIds.Contains(x.EmployeeId))
			.ToList();
		var itemsByEmployee = snapshots
			.Select(snapshot =>
			{
				var item = PayrollRunProjection.ToItem(snapshot);
				item.IssueCodes = item.IssueCodes
					.Concat(new[] { IssueCodes.PayrollPolicyConfiguration })
					.Distinct()
					.ToList();
				return item;
			})
			.ToDictionary(x => x.EmployeeId, x => x);

		foreach (var employee in scopedEmployees.Where(x => !itemsByEmployee.ContainsKey(x.Id)))
		{
			itemsByEmployee[employee.Id] = BuildFallbackItem(employee, year, month);
		}

		var existingSlips = await salarySlipRepository.GetByMonthAsync(internalMonthYear);
		var lockedCount = existingSlips.Count(x =>
			x.Status == SalarySlipStatus.Finalized && scopedEmployeeIds.Contains(x.EmployeeId));

		return new PolicySetupFallbackItems(
			itemsByEmployee.Values.OrderBy(x => x.EmployeeName).ToList(),
			scopedEmployeeIds.Count,
			lockedCount,
			lockedCount > 0);
	}

	private static RunPayrollItem BuildFallbackItem(Employee employee, int year, int month)
	{
		var daysInMonth = DateTime.DaysInMonth(year, month);
		return new RunPayrollItem
		{
			EmployeeId = employee.Id,
			IsLocked = false,
			ReviewStatus = ReviewStatuses.Pending,
			EmployeeName = PayrollRunRules.FormatEmployeeName(employee),
			EmployeeIdentifier = employee.EmployeeCode ?? string.Empty,
			IsTeaching = employee.EmployeeGroup == EmployeeGroupType.Teaching,
			IssueCodes = new List<string> { IssueCodes.PayrollPolicyConfiguration },
			TotalWorkingDays = daysInMonth,
			DayCalculationMode = PayrollDayCalculationApiLabels.ToApiString(DayCalculationType.TotalMonthDays),
			UsedDefaultDayCalculation = true,
			HolidayImpactJson = "[]",
			BasisDays = daysInMonth,
			PayrollAttendanceSource = "raw_attendance"
		};
	}

	private sealed record PolicySetupFallbackItems(
		List<RunPayrollItem> Items,
		int EmployeeCount,
		int LockedCount,
		bool IsGenerated);
}
