using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.AttendanceAndLeaves.YearEndSettlements;

public class LeaveYearEndSettlementWorkflow(
	IRepository<LeaveYearEndSettlementRun, Guid> runRepository,
	IRepository<LeaveYearEndSettlementItem, Guid> itemRepository,
	IAcademicYearRepository academicYearRepository,
	IEmployeeRepository employeeRepository,
	LeaveBalanceUnitService leaveBalanceUnitService,
	IEmployeePayrollComponentRepository payrollComponentRepository,
	IRepository<PayrollRunItemSnapshot, Guid> payrollRunItemSnapshotRepository,
	HrPolicyResolver hrPolicyResolver,
	LeaveCarryForwardBalanceReader carryForwardBalanceReader,
	PayrollComponentVersionResolver componentVersionResolver,
	PayrollCalendarResolver payrollCalendarResolver,
	PayrollBasisPreviewResolver payrollBasisPreviewResolver,
	PayrollAdjustmentWorkflow payrollAdjustmentWorkflow,
	PayrollSnapshotRefresher payrollSnapshotRefresher,
	IGuidGenerator guidGenerator,
	ICurrentTenant currentTenant,
	IClock clock,
	IAsyncQueryableExecuter asyncExecuter,
	IUnitOfWorkManager unitOfWorkManager) : ITransientDependency
{
	private const string LeaveEncashmentAdjustmentName = "Leave Encashment";

	public async Task<List<LeaveYearEndSettlementRunDto>> GetListAsync(Guid academicYearId)
	{
		var query = await runRepository.GetQueryableAsync();
		var runs = await asyncExecuter.ToListAsync(query
			.Where(x => x.AcademicYearId == academicYearId)
			.OrderByDescending(x => x.PreviewedAt));

		return runs.Select(run => MapRun(run, Array.Empty<LeaveYearEndSettlementItem>(), new Dictionary<Guid, Employee>()))
			.ToList();
	}

	public async Task<LeaveYearEndSettlementRunDto> GetAsync(Guid runId)
	{
		var run = await GetRunOrThrowAsync(runId);
		var items = await GetItemsAsync(run.Id);
		var employeeLookup = await GetEmployeeLookupAsync(items.Select(x => x.EmployeeId).ToHashSet());
		return MapRun(run, items, employeeLookup);
	}

	public async Task<LeaveYearEndSettlementRunDto> PreviewAsync(Guid academicYearId)
	{
		var sourceYear = await academicYearRepository.GetAsync(academicYearId);
		var targetYear = await FindTargetAcademicYearAsync(sourceYear);
		var run = await FindRunAsync(sourceYear.Id);
		if (run?.Status == LeaveYearEndSettlementRunStatus.Finalized)
		{
			var finalizedItems = await GetItemsAsync(run.Id);
			var finalizedEmployeeLookup = await GetEmployeeLookupAsync(finalizedItems.Select(x => x.EmployeeId).ToHashSet());
			return MapRun(run, finalizedItems, finalizedEmployeeLookup);
		}

		run ??= new LeaveYearEndSettlementRun(
			guidGenerator.Create(),
			currentTenant.Id,
			sourceYear.Id,
			targetYear?.Id,
			clock.Now);

		if (run.Id == default)
		{
			throw new BusinessException(HRErrorCodes.LeaveYearEndSettlementNotFound);
		}

		var (draftItems, employees) = await RefreshDraftRunAsync(run, sourceYear, targetYear, clock.Now);
		var employeeLookup = employees.ToDictionary(x => x.Id);
		return MapRun(run, draftItems, employeeLookup);
	}

	public async Task<LeaveYearEndSettlementRunDto> FinalizeAsync(Guid runId)
	{
		var run = await GetRunOrThrowAsync(runId);
		if (run.Status == LeaveYearEndSettlementRunStatus.Finalized)
		{
			return await GetAsync(runId);
		}

		var sourceYear = await academicYearRepository.GetAsync(run.AcademicYearId);
		var targetYear = await FindTargetAcademicYearAsync(sourceYear);
		var (items, _) = await RefreshDraftRunAsync(run, sourceYear, targetYear, clock.Now);
		if (items.Count == 0 || items.Any(x => x.HasIssues))
		{
			run.Refresh(targetYear?.Id, run.PreviewedAt, items);
			await runRepository.UpdateAsync(run, autoSave: true);
			throw new BusinessException(HRErrorCodes.LeaveYearEndSettlementFinalizeBlocked)
				.WithData("runId", run.Id);
		}

		var blockedByRuntimeIssue = false;
		foreach (var item in items)
		{
			var runtimeIssues = await ResolveFinalizationIssuesAsync(item);
			if (runtimeIssues.Count > 0)
			{
				item.SetIssueCodes(runtimeIssues);
				await itemRepository.UpdateAsync(item, autoSave: false);
				blockedByRuntimeIssue = true;
			}
		}

		if (blockedByRuntimeIssue)
		{
			await unitOfWorkManager.Current!.SaveChangesAsync();
			items = await GetItemsAsync(run.Id);
			run.Refresh(targetYear?.Id, run.PreviewedAt, items);
			await runRepository.UpdateAsync(run, autoSave: true);

			throw new BusinessException(HRErrorCodes.LeaveYearEndSettlementFinalizeBlocked)
				.WithData("runId", run.Id);
		}

		var refreshedPayrollKeys = new Dictionary<string, HashSet<Guid>>();
		foreach (var item in items)
		{
			Guid? adjustmentId = null;
			if (item.EncashedDays > 0m && !string.IsNullOrWhiteSpace(item.PayrollMonth))
			{
				var adjustment = await payrollAdjustmentWorkflow.UpsertSystemGeneratedAsync(
					item.PayrollMonth,
					item.EmployeeId,
					LeaveEncashmentAdjustmentName,
					item.Amount,
					isEarning: true,
					reason: $"Year-end leave encashment for {item.EncashedDays:0.##} days",
					description: $"Academic year leave settlement {run.AcademicYearId}",
					PayrollAdjustmentSourceTypes.LeaveYearEndEncashment,
					item.Id);
				adjustmentId = adjustment.Id;

				if (!refreshedPayrollKeys.TryGetValue(item.PayrollMonth, out var employeeIds))
				{
					employeeIds = new HashSet<Guid>();
					refreshedPayrollKeys[item.PayrollMonth] = employeeIds;
				}

				employeeIds.Add(item.EmployeeId);
			}

			item.MarkFinalized(adjustmentId);
			await itemRepository.UpdateAsync(item, autoSave: false);
		}

		await unitOfWorkManager.Current!.SaveChangesAsync();
		foreach (var (monthYear, employeeIds) in refreshedPayrollKeys)
		{
			var (year, month, internalMonthYear) = PayrollRunRules.ParseMonth(monthYear);
			await payrollSnapshotRefresher.RefreshForEmployeeIdsAsync(
				internalMonthYear,
				year,
				month,
				null,
				employeeIds,
				preserveReviewedSnapshots: false,
				enforceUserScope: false);
		}

		items = await GetItemsAsync(run.Id);
		run.MarkFinalized(clock.Now, items);
		await runRepository.UpdateAsync(run, autoSave: true);

		var employeeLookup = await GetEmployeeLookupAsync(items.Select(x => x.EmployeeId).ToHashSet());
		return MapRun(run, items, employeeLookup);
	}

	private async Task<(List<LeaveYearEndSettlementItem> Items, List<Employee> Employees)> RefreshDraftRunAsync(
		LeaveYearEndSettlementRun run,
		AcademicYear sourceYear,
		AcademicYear? targetYear,
		DateTime previewedAt)
	{
		var isNew = await FindRunAsync(sourceYear.Id) == null;
		if (isNew)
		{
			await runRepository.InsertAsync(run, autoSave: true);
		}

		var employees = await GetActiveEmployeesForYearEndAsync(sourceYear);
		var draftItems = await BuildDraftItemsAsync(run, sourceYear, targetYear, employees);
		await ReplaceDraftItemsAsync(run.Id, draftItems);
		run.Refresh(targetYear?.Id, previewedAt, draftItems);
		await runRepository.UpdateAsync(run, autoSave: true);
		return (draftItems, employees);
	}

	private async Task<List<LeaveYearEndSettlementItem>> BuildDraftItemsAsync(
		LeaveYearEndSettlementRun run,
		AcademicYear sourceYear,
		AcademicYear? targetYear,
		IReadOnlyCollection<Employee> employees)
	{
		var result = new List<LeaveYearEndSettlementItem>();
		var existingByKey = (await GetItemsAsync(run.Id))
			.Where(x => x.Status != LeaveYearEndSettlementItemStatus.Finalized)
			.ToDictionary(x => (x.EmployeeId, x.SourceLeavePolicyRuleId));

		foreach (var employee in employees)
		{
			HrResolvedPolicies sourcePolicies;
			try
			{
				sourcePolicies = await hrPolicyResolver.ResolveAsync(employee.Id, sourceYear.EndDate.Date, sourceYear.Id);
			}
			catch (BusinessException ex) when (IsPolicyResolutionSkip(ex))
			{
				continue;
			}

			var sourcePolicy = sourcePolicies.LeavePolicy;
			var sourceRule = sourcePolicy?.Rules.FirstOrDefault(rule =>
				rule.IsActive &&
				rule.PaidType == LeavePaidType.Paid &&
				!rule.IsUnlimited &&
				(rule.CarryForward || rule.Encashable));
			if (sourcePolicy == null || sourceRule == null)
			{
				continue;
			}

			var issues = new List<string>();
			var unitsByRule = await leaveBalanceUnitService.GetBalanceUnitsByRuleAsync(
				employee.Id,
				new[] { sourceRule.Id });
			unitsByRule.TryGetValue(sourceRule.Id, out var units);
			var approvedUsed = units?.ApprovedDays ?? 0m;
			var pending = units?.PendingDays ?? 0m;
			if (pending > 0m)
			{
				issues.Add(LeaveYearEndSettlementIssueCodes.PendingLeave);
			}

			var carryIn = (await carryForwardBalanceReader.GetCarryInByRuleAsync(
					employee.Id,
					sourceYear.Id,
					new[] { sourceRule.Id }))
				.GetValueOrDefault(sourceRule.Id);
			var accrued = LeaveAccrualCalculator.CalculateAccruedDays(
				sourcePolicy,
				sourceRule,
				sourceYear.StartDate,
				sourceYear.EndDate,
				employee.DateOfJoining,
				sourceYear.EndDate.Date) ?? 0m;
			var available = accrued + carryIn;
			var unused = Math.Max(0m, available - approvedUsed);
			var carryLimit = sourceRule.CarryForwardLimit.HasValue
				? Math.Max(0m, sourceRule.CarryForwardLimit.Value)
				: unused;
			var intendedCarry = sourceRule.CarryForward ? Math.Min(unused, carryLimit) : 0m;
			Guid? targetRuleId = null;
			var carryIssues = new List<string>();
			if (intendedCarry > 0m)
			{
				if (targetYear == null)
				{
					carryIssues.Add(LeaveYearEndSettlementIssueCodes.MissingTargetAcademicYear);
				}
				else
				{
					targetRuleId = await ResolveTargetPaidRuleIdAsync(employee.Id, targetYear, carryIssues);
				}

				if (targetRuleId == null)
				{
					intendedCarry = 0m;
					if (!sourceRule.Encashable)
					{
						issues.AddRange(carryIssues);
					}
				}
			}

			var encashedDays = sourceRule.Encashable
				? Math.Max(0m, unused - intendedCarry)
				: 0m;
			var payrollMonth = encashedDays > 0m
				? ResolvePayrollMonth(sourceRule.EncashmentTiming, sourceYear.EndDate)
				: null;
			var amount = 0m;
			if (encashedDays > 0m && payrollMonth != null)
			{
				amount = await CalculateEncashmentAmountAsync(employee, payrollMonth, encashedDays, issues);
			}

			var forfeited = Math.Max(0m, unused - intendedCarry - encashedDays);
			var key = (employee.Id, sourceRule.Id);
			var item = existingByKey.TryGetValue(key, out var existing)
				? existing
				: new LeaveYearEndSettlementItem(
					guidGenerator.Create(),
					currentTenant.Id,
					run.Id,
					sourceYear.Id,
					targetYear?.Id,
					employee.Id,
					sourcePolicy.Id,
					sourceRule.Id);

			item.UpdateDraft(
				targetYear?.Id,
				targetRuleId,
				RoundDays(accrued),
				RoundDays(carryIn),
				RoundDays(approvedUsed),
				RoundDays(pending),
				RoundDays(unused),
				RoundDays(intendedCarry),
				RoundDays(encashedDays),
				RoundDays(forfeited),
				payrollMonth,
				Math.Round(amount, 2, MidpointRounding.AwayFromZero),
				issues);
			result.Add(item);
		}

		return result;
	}

	private async Task<decimal> CalculateEncashmentAmountAsync(
		Employee employee,
		string payrollMonth,
		decimal encashedDays,
		List<string> issues)
	{
		try
		{
			var (year, month, internalMonthYear) = PayrollRunRules.ParseMonth(payrollMonth);
			var calendar = await payrollCalendarResolver.ResolveAsync(year, month);
			var resolvedPolicies = await hrPolicyResolver.ResolveAsync(
				employee.Id,
				PayrollRunRules.GetPayrollPolicyResolutionDate(year, month),
				calendar.AcademicYearId);
			var holidayOverrides = resolvedPolicies.PayrollPolicy.GetHolidayOverrides();
			var workCalendarHolidays = await payrollBasisPreviewResolver.GetIncludedWorkCalendarHolidaysAsync(
				resolvedPolicies.PayrollWorkCalendarPolicy.Id,
				year,
				month);
			var workingUnitsByDate = PayrollMonthCalendar.BuildWorkingUnitsByDate(
				year,
				month,
				workCalendarHolidays,
				resolvedPolicies.PayrollPolicy.SaturdayWorkPolicy,
				holidayOverrides,
				resolvedPolicies.PayrollWorkCalendarPolicy);
			var payrollPolicyIssues = new List<string>();
			var basis = payrollBasisPreviewResolver.Resolve(
				resolvedPolicies.PayrollPolicy,
				year,
				month,
				workingUnitsByDate,
				payrollPolicyIssues);
			if (basis.BasisDays <= 0m)
			{
				issues.Add(LeaveYearEndSettlementIssueCodes.PayrollBasisMissing);
				return 0m;
			}

			var componentsByEmployee = await payrollComponentRepository.GetByEmployeeIdsAsync(new[] { employee.Id });
			var resolvedComponentsByEmployee = await componentVersionResolver.ResolveByEmployeeAsync(
				componentsByEmployee,
				internalMonthYear);
			var components = PayrollComponentMonthScope.FilterForMonth(
				resolvedComponentsByEmployee[employee.Id],
				year,
				month);
			var fullGross = components.Where(x => x.IsEarning).Sum(x => x.Amount);
			if (fullGross <= 0m)
			{
				issues.Add(LeaveYearEndSettlementIssueCodes.PayrollGrossMissing);
				return 0m;
			}

			return Math.Round(fullGross / basis.BasisDays * encashedDays, 2, MidpointRounding.AwayFromZero);
		}
		catch (BusinessException ex) when (PayrollRunRules.IsPayrollPolicySetupException(ex))
		{
			issues.Add(LeaveYearEndSettlementIssueCodes.PayrollPreviewRequired);
			return 0m;
		}
	}

	private async Task<Guid?> ResolveTargetPaidRuleIdAsync(
		Guid employeeId,
		AcademicYear targetYear,
		List<string> issues)
	{
		try
		{
			var targetPolicies = await hrPolicyResolver.ResolveAsync(employeeId, targetYear.StartDate.Date, targetYear.Id);
			var targetRule = targetPolicies.LeavePolicy?.Rules.FirstOrDefault(rule =>
				rule.IsActive &&
				rule.PaidType == LeavePaidType.Paid &&
				!rule.IsUnlimited);
			if (targetRule == null)
			{
				issues.Add(LeaveYearEndSettlementIssueCodes.MissingTargetPaidRule);
				return null;
			}

			return targetRule.Id;
		}
		catch (BusinessException ex) when (IsPolicyResolutionSkip(ex))
		{
			issues.Add(LeaveYearEndSettlementIssueCodes.MissingTargetPaidRule);
			return null;
		}
	}

	private async Task<List<string>> ResolveFinalizationIssuesAsync(LeaveYearEndSettlementItem item)
	{
		var issues = item.GetIssueCodes().ToList();
		if (item.EncashedDays <= 0m || string.IsNullOrWhiteSpace(item.PayrollMonth))
		{
			return issues;
		}

		var query = await payrollRunItemSnapshotRepository.GetQueryableAsync();
		var snapshot = await asyncExecuter.FirstOrDefaultAsync(query.Where(x =>
			x.MonthYear == item.PayrollMonth &&
			x.EmployeeId == item.EmployeeId));
		if (snapshot?.IsLocked == true)
		{
			issues.Add(LeaveYearEndSettlementIssueCodes.PayrollLocked);
		}

		if (item.Amount <= 0m)
		{
			issues.Add(LeaveYearEndSettlementIssueCodes.PayrollGrossMissing);
		}

		return issues.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
	}

	private async Task ReplaceDraftItemsAsync(Guid runId, IReadOnlyCollection<LeaveYearEndSettlementItem> nextItems)
	{
		var existing = await GetItemsAsync(runId);
		var nextIds = nextItems.Select(x => x.Id).ToHashSet();
		foreach (var stale in existing.Where(x =>
			         x.Status != LeaveYearEndSettlementItemStatus.Finalized &&
			         !nextIds.Contains(x.Id)))
		{
			await itemRepository.DeleteAsync(stale, autoSave: false);
		}

		foreach (var item in nextItems)
		{
			if (existing.Any(x => x.Id == item.Id))
			{
				await itemRepository.UpdateAsync(item, autoSave: false);
			}
			else
			{
				await itemRepository.InsertAsync(item, autoSave: false);
			}
		}

		await unitOfWorkManager.Current!.SaveChangesAsync();
	}

	private async Task<LeaveYearEndSettlementRun?> FindRunAsync(Guid academicYearId)
	{
		var query = await runRepository.GetQueryableAsync();
		return await asyncExecuter.FirstOrDefaultAsync(query.Where(x => x.AcademicYearId == academicYearId));
	}

	private async Task<LeaveYearEndSettlementRun> GetRunOrThrowAsync(Guid runId)
	{
		var query = await runRepository.GetQueryableAsync();
		return await asyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == runId))
			?? throw new BusinessException(HRErrorCodes.LeaveYearEndSettlementNotFound)
				.WithData("runId", runId);
	}

	private async Task<List<LeaveYearEndSettlementItem>> GetItemsAsync(Guid runId)
	{
		var query = await itemRepository.GetQueryableAsync();
		return await asyncExecuter.ToListAsync(query
			.Where(x => x.RunId == runId)
			.OrderBy(x => x.CreationTime));
	}

	private async Task<AcademicYear?> FindTargetAcademicYearAsync(AcademicYear sourceYear)
	{
		var query = await academicYearRepository.GetQueryableAsync();
		return await asyncExecuter.FirstOrDefaultAsync(query
			.Where(x => x.StartDate.Date > sourceYear.EndDate.Date)
			.OrderBy(x => x.StartDate));
	}

	private async Task<List<Employee>> GetActiveEmployeesForYearEndAsync(AcademicYear academicYear)
	{
		var targetDate = DateOnly.FromDateTime(academicYear.EndDate.Date);
		var query = await employeeRepository.GetQueryableAsync();
		var employees = await asyncExecuter.ToListAsync(query.Where(x =>
			!x.DateOfJoining.HasValue || x.DateOfJoining.Value <= targetDate));
		return employees
			.Where(employee => EmployeeLifecycleEligibility.IsAttendanceEligibleOnDate(
				employee.EmployeeStatusType,
				employee.StatusEffectiveDate,
				employee.StatusLastChangedAt,
				targetDate))
			.OrderBy(x => x.EmployeeName)
			.ThenBy(x => x.EmployeeCode)
			.ToList();
	}

	private async Task<Dictionary<Guid, Employee>> GetEmployeeLookupAsync(IReadOnlyCollection<Guid> employeeIds)
	{
		if (employeeIds.Count == 0)
		{
			return new Dictionary<Guid, Employee>();
		}

		var query = await employeeRepository.GetQueryableAsync();
		return (await asyncExecuter.ToListAsync(query.Where(x => employeeIds.Contains(x.Id))))
			.ToDictionary(x => x.Id);
	}

	private static LeaveYearEndSettlementRunDto MapRun(
		LeaveYearEndSettlementRun run,
		IReadOnlyCollection<LeaveYearEndSettlementItem> items,
		IReadOnlyDictionary<Guid, Employee> employeeLookup) =>
		new()
		{
			Id = run.Id,
			AcademicYearId = run.AcademicYearId,
			TargetAcademicYearId = run.TargetAcademicYearId,
			Status = run.Status.ToString(),
			PreviewedAt = run.PreviewedAt,
			FinalizedAt = run.FinalizedAt,
			TotalItems = run.TotalItems,
			FinalizedItems = run.FinalizedItems,
			BlockedItems = run.BlockedItems,
			TotalCarriedDays = run.TotalCarriedDays,
			TotalEncashedDays = run.TotalEncashedDays,
			TotalForfeitedDays = run.TotalForfeitedDays,
			TotalEncashmentAmount = run.TotalEncashmentAmount,
			Items = items.Select(item => MapItem(item, employeeLookup.GetValueOrDefault(item.EmployeeId))).ToList()
		};

	private static LeaveYearEndSettlementItemDto MapItem(LeaveYearEndSettlementItem item, Employee? employee) =>
		new()
		{
			Id = item.Id,
			RunId = item.RunId,
			SourceAcademicYearId = item.SourceAcademicYearId,
			TargetAcademicYearId = item.TargetAcademicYearId,
			EmployeeId = item.EmployeeId,
			EmployeeName = employee?.EmployeeName ?? string.Empty,
			EmployeeCode = employee?.EmployeeCode ?? string.Empty,
			SourceLeavePolicyId = item.SourceLeavePolicyId,
			SourceLeavePolicyRuleId = item.SourceLeavePolicyRuleId,
			TargetLeavePolicyRuleId = item.TargetLeavePolicyRuleId,
			AccruedDays = item.AccruedDays,
			CarryInDays = item.CarryInDays,
			ApprovedUsedDays = item.ApprovedUsedDays,
			PendingDays = item.PendingDays,
			UnusedDays = item.UnusedDays,
			CarriedDays = item.CarriedDays,
			EncashedDays = item.EncashedDays,
			ForfeitedDays = item.ForfeitedDays,
			PayrollMonth = item.PayrollMonth,
			Amount = item.Amount,
			Status = item.Status.ToString(),
			IssueCodes = item.GetIssueCodes().ToList(),
			PayrollAdjustmentId = item.PayrollAdjustmentId
		};

	private static string? ResolvePayrollMonth(LeaveEncashmentTiming? timing, DateTime academicYearEnd)
	{
		if (!timing.HasValue)
		{
			return null;
		}

		var month = new DateTime(academicYearEnd.Year, academicYearEnd.Month, 1);
		if (timing.Value == LeaveEncashmentTiming.NextPayroll)
		{
			month = month.AddMonths(1);
		}

		return month.ToString("yyyy-MM");
	}

	private static bool IsPolicyResolutionSkip(BusinessException ex) =>
		ex.Code is
			HRErrorCodes.DefaultLeavePolicyRequired or
			HRErrorCodes.LeavePolicyNotFound or
			HRErrorCodes.LeavePolicyInactive or
			HRErrorCodes.HrPolicyAssignmentAcademicYearMismatch;

	private static decimal RoundDays(decimal value) =>
		Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
