using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.HR;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Audit;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

/// <summary>
/// Application service for running payroll - generates preview and locks payroll for a month
/// </summary>
[RemoteService(false)]
public class RunPayrollAppService : ApplicationService, IRunPayrollAppService
{
	private sealed record PayrollTargetEmployee(Guid Id, string? EmployeeCode, string? EmployeeName);
	private static readonly ConcurrentDictionary<string, SemaphoreSlim> SnapshotRefreshLocks = new();

	private static class ReviewStatuses
	{
		public const string Pending = "PENDING";
		public const string Reviewed = "REVIEWED";
		public const string Locked = "LOCKED";
	}

	private static class IssueCodes
	{
		public const string MissingSalaryStructure = "MISSING_SALARY_STRUCTURE";
		public const string NoAttendance = "NO_ATTENDANCE";
		public const string LowAttendance = "LOW_ATTENDANCE";
		public const string NegativeNetPay = "NEGATIVE_NET_PAY";
		public const string NoPreviousMonthComparison = "NO_PREVIOUS_MONTH_COMPARISON";
		public const string LargeSalaryVariance = "LARGE_SALARY_VARIANCE";
		public const string AttendancePolicyEffectiveBoundary = "ATTENDANCE_POLICY_EFFECTIVE_BOUNDARY";
		public const string PayrollPolicyConfiguration = "PAYROLL_POLICY_CONFIGURATION";
		public const string NoWorkingDays = "NO_WORKING_DAYS";
	}

	private readonly IRepository<EmployeeAttendance, Guid> _attendanceRepository;
	private readonly IEmployeePayrollDetailsRepository _payrollDetailsRepository;
	private readonly IEmployeePayrollComponentRepository _payrollComponentRepository;
	private readonly ISalarySlipRepository _salarySlipRepository;
	private readonly IRepository<PayrollRunReview, Guid> _payrollRunReviewRepository;
	private readonly IRepository<PayrollRunItemSnapshot, Guid> _payrollRunItemSnapshotRepository;
	private readonly IRepository<PayrollAdjustment, Guid> _payrollAdjustmentRepository;
	private readonly IRepository<WorkCalendarHoliday, Guid> _workCalendarHolidayRepository;
	private readonly IAcademicYearRepository _academicYearRepository;
	private readonly HrPolicyResolver _hrPolicyResolver;
	private readonly PayrollCalendarResolver _payrollCalendarResolver;
	private readonly PayrollApprovedLeaveResolver _payrollApprovedLeaveResolver;
	private readonly SalarySlipManager _salarySlipManager;
	private readonly PayrollComponentVersionResolver _componentVersionResolver;
	private readonly IUserScopeService _userScopeService;
	private readonly HrEmployeeScopeQueryService _hrEmployeeScopeQueryService;
	private readonly HrAuditWriter _auditWriter;
	private readonly ILogger<RunPayrollAppService> _logger;

	// Configuration: Batch size for processing employees
	private const int DefaultBatchSize = 50;
	private const int MaxBatchSize = 100;

	public RunPayrollAppService(
		IRepository<EmployeeAttendance, Guid> attendanceRepository,
		IEmployeePayrollDetailsRepository payrollDetailsRepository,
		IEmployeePayrollComponentRepository payrollComponentRepository,
		ISalarySlipRepository salarySlipRepository,
		IRepository<PayrollRunReview, Guid> payrollRunReviewRepository,
		IRepository<PayrollRunItemSnapshot, Guid> payrollRunItemSnapshotRepository,
		IRepository<PayrollAdjustment, Guid> payrollAdjustmentRepository,
		IRepository<WorkCalendarHoliday, Guid> workCalendarHolidayRepository,
		IAcademicYearRepository academicYearRepository,
		HrPolicyResolver hrPolicyResolver,
		PayrollCalendarResolver payrollCalendarResolver,
		PayrollApprovedLeaveResolver payrollApprovedLeaveResolver,
		SalarySlipManager salarySlipManager,
		PayrollComponentVersionResolver componentVersionResolver,
		IUserScopeService userScopeService,
		HrEmployeeScopeQueryService hrEmployeeScopeQueryService,
		HrAuditWriter auditWriter,
		ILogger<RunPayrollAppService> logger)
	{
		_attendanceRepository = attendanceRepository;
		_payrollDetailsRepository = payrollDetailsRepository;
		_payrollComponentRepository = payrollComponentRepository;
		_salarySlipRepository = salarySlipRepository;
		_payrollRunReviewRepository = payrollRunReviewRepository;
		_payrollRunItemSnapshotRepository = payrollRunItemSnapshotRepository;
		_payrollAdjustmentRepository = payrollAdjustmentRepository;
		_workCalendarHolidayRepository = workCalendarHolidayRepository;
		_academicYearRepository = academicYearRepository;
		_hrPolicyResolver = hrPolicyResolver;
		_payrollCalendarResolver = payrollCalendarResolver;
		_payrollApprovedLeaveResolver = payrollApprovedLeaveResolver;
		_salarySlipManager = salarySlipManager;
		_componentVersionResolver = componentVersionResolver;
		_userScopeService = userScopeService;
		_hrEmployeeScopeQueryService = hrEmployeeScopeQueryService;
		_auditWriter = auditWriter;
		_logger = logger;
	}

	/// <summary>
	/// Get payroll preview for a specific month (paginated)
	/// </summary>
	public async Task<RunPayrollResponse> GetPreviewAsync(GetRunPayrollInput input)
	{
		// Validate input
		if (input.PageSize <= 0 || input.PageSize > 100)
		{
			throw new BusinessException(HRErrorCodes.PayrollInvalidPageSize)
				.WithData("pageSize", input.PageSize)
				.WithData("maxPageSize", 100);
		}

		try
		{
			// Parse and validate month
			var (year, month, internalMonthYear) = ParseMonth(input.Month);
			var academicYearId = await ResolveAcademicYearScopeAsync(input.AcademicYearId, year, month);

			_logger.LogInformation(
				"Generating payroll preview for {Month} (Year: {Year}, Month: {MonthNum}, Page: {Page}, PageSize: {PageSize})",
				input.Month, year, month, input.Page, input.PageSize);

			return await WithPayrollSnapshotLockAsync(internalMonthYear, async () =>
			{
				// Get payroll-eligible employees in caller scope for the selected month.
				var allScopedEmployees = await GetScopedEmployeesAsync(includeUnknownStageEmployees: false);
				var scopedEmployees = await GetScopedPayrollEmployeesAsync(year, month, includeUnknownStageEmployees: false);
				await CleanupIneligiblePayrollRowsAsync(internalMonthYear, allScopedEmployees, scopedEmployees);
				var scopedEmployeeIds = scopedEmployees.Select(e => e.Id).ToHashSet();

				var existingSlips = await _salarySlipRepository.GetByMonthAsync(internalMonthYear);
				var finalizedEmployeeIds = existingSlips
					.Where(x => x.Status == SalarySlipStatus.Finalized)
					.Select(x => x.EmployeeId)
					.ToHashSet();
				var isGenerated = finalizedEmployeeIds.Count > 0;

				var persistedSnapshots = await GetSnapshotsByMonthAsync(internalMonthYear);
				var inScopeSnapshots = persistedSnapshots
					.Where(x => scopedEmployeeIds.Contains(x.EmployeeId))
					.OrderBy(x => x.EmployeeName)
					.ToList();
				if (input.ForceRefresh ||
				    inScopeSnapshots.Count < scopedEmployees.Count ||
				    await HaveSnapshotInvalidationsAsync(internalMonthYear, year, month, academicYearId, scopedEmployeeIds, inScopeSnapshots))
				{
					await RefreshSnapshotsAsync(internalMonthYear, year, month, academicYearId, scopedEmployees, preserveReviewedSnapshots: false);
					persistedSnapshots = await GetSnapshotsByMonthAsync(internalMonthYear);
					inScopeSnapshots = persistedSnapshots
						.Where(x => scopedEmployeeIds.Contains(x.EmployeeId))
						.OrderBy(x => x.EmployeeName)
						.ToList();
				}

				var totalCount = inScopeSnapshots.Count;
				var items = inScopeSnapshots
					.Skip(input.Page * input.PageSize)
					.Take(input.PageSize)
					.Select(MapSnapshotToItem)
					.ToList();
				var readinessItems = inScopeSnapshots.Select(MapSnapshotToItem).ToList();

				return new RunPayrollResponse
				{
					IsGenerated = isGenerated,
					TotalCount = totalCount,
					CurrentPage = input.Page,
					PageSize = input.PageSize,
					TotalPages = (int)Math.Ceiling((double)totalCount / input.PageSize),
					Items = items,
					Readiness = BuildReadinessSummary(internalMonthYear, readinessItems, isGenerated)
				};
			});
		}
		catch (BusinessException ex) when (IsPayrollPolicySetupException(ex))
		{
			_logger.LogWarning(
				ex,
				"Payroll preview setup issue escaped snapshot refresh for month {Month}. Returning policy setup fallback response. ErrorCode={ErrorCode}",
				input.Month,
				ex.Code);
			return await BuildPolicySetupPreviewFallbackAsync(input);
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex) when (IsSnapshotSchemaMissing(ex))
		{
			_logger.LogError(ex, "Payroll snapshot schema missing for month {Month}. Migration required.", input.Month);
			throw new BusinessException(HRErrorCodes.PayrollSnapshotMigrationRequired)
				.WithData("month", input.Month)
				.WithData("requiredTable", "HR_PayrollRunItems")
				.WithData("requiredMigration", "20260428034756_PayrollRunItemSnapshots");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected error generating payroll preview for month {Month}", input.Month);
			throw new BusinessException(HRErrorCodes.PayrollPreviewFailed)
				.WithData("month", input.Month);
		}
	}

	public async Task<RunPayrollReadinessSummary> GetReadinessAsync(string month, Guid? academicYearId = null)
	{
		try
		{
			var (year, monthNumber, internalMonthYear) = ParseMonth(month);
			var resolvedAcademicYearId = await ResolveAcademicYearScopeAsync(academicYearId, year, monthNumber);
			return await WithPayrollSnapshotLockAsync(internalMonthYear, async () =>
			{
				var allScopedEmployees = await GetScopedEmployeesAsync(includeUnknownStageEmployees: false);
				var scopedEmployees = await GetScopedPayrollEmployeesAsync(year, monthNumber, includeUnknownStageEmployees: false);
				await CleanupIneligiblePayrollRowsAsync(internalMonthYear, allScopedEmployees, scopedEmployees);
				var scopedEmployeeIds = scopedEmployees.Select(e => e.Id).ToHashSet();

				var snapshots = (await GetSnapshotsByMonthAsync(internalMonthYear))
					.Where(x => scopedEmployeeIds.Contains(x.EmployeeId))
					.ToList();
				if (snapshots.Count < scopedEmployees.Count ||
				    await HaveSnapshotInvalidationsAsync(internalMonthYear, year, monthNumber, resolvedAcademicYearId, scopedEmployeeIds, snapshots))
				{
					await RefreshSnapshotsAsync(internalMonthYear, year, monthNumber, resolvedAcademicYearId, scopedEmployees, preserveReviewedSnapshots: false);
					snapshots = (await GetSnapshotsByMonthAsync(internalMonthYear))
						.Where(x => scopedEmployeeIds.Contains(x.EmployeeId))
						.ToList();
				}

				var items = snapshots.Select(MapSnapshotToItem).ToList();
				var existingSlips = await _salarySlipRepository.GetByMonthAsync(internalMonthYear);
				var lockedCount = existingSlips.Count(x =>
					x.Status == SalarySlipStatus.Finalized && scopedEmployeeIds.Contains(x.EmployeeId));

				return BuildReadinessSummary(
					internalMonthYear,
					items,
					isGenerated: lockedCount > 0,
					employeeCountOverride: scopedEmployeeIds.Count,
					lockedCountOverride: lockedCount);
			});
		}
		catch (BusinessException ex) when (IsPayrollPolicySetupException(ex))
		{
			_logger.LogWarning(
				ex,
				"Payroll readiness setup issue escaped snapshot refresh for month {Month}. Returning policy setup fallback response. ErrorCode={ErrorCode}",
				month,
				ex.Code);
			return await BuildPolicySetupReadinessFallbackAsync(month, academicYearId);
		}
	}

	private async Task<RunPayrollResponse> BuildPolicySetupPreviewFallbackAsync(GetRunPayrollInput input)
	{
		var (year, month, internalMonthYear) = ParseMonth(input.Month);
		await ResolveAcademicYearScopeAsync(input.AcademicYearId, year, month);
		var fallback = await BuildPolicySetupFallbackItemsAsync(internalMonthYear, year, month);
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
			Readiness = BuildReadinessSummary(
				internalMonthYear,
				fallback.Items,
				fallback.IsGenerated,
				employeeCountOverride: fallback.EmployeeCount,
				lockedCountOverride: fallback.LockedCount)
		};
	}

	private async Task<RunPayrollReadinessSummary> BuildPolicySetupReadinessFallbackAsync(string month, Guid? academicYearId = null)
	{
		var (year, monthNumber, internalMonthYear) = ParseMonth(month);
		await ResolveAcademicYearScopeAsync(academicYearId, year, monthNumber);
		var fallback = await BuildPolicySetupFallbackItemsAsync(internalMonthYear, year, monthNumber);
		return BuildReadinessSummary(
			internalMonthYear,
			fallback.Items,
			fallback.IsGenerated,
			employeeCountOverride: fallback.EmployeeCount,
			lockedCountOverride: fallback.LockedCount);
	}

	private async Task<PolicySetupFallbackItems> BuildPolicySetupFallbackItemsAsync(
		string internalMonthYear,
		int year,
		int month)
	{
		var allScopedEmployees = await GetScopedEmployeesAsync(includeUnknownStageEmployees: false);
		var scopedEmployees = await GetScopedPayrollEmployeesAsync(year, month, includeUnknownStageEmployees: false);
		await CleanupIneligiblePayrollRowsAsync(internalMonthYear, allScopedEmployees, scopedEmployees);
		var scopedEmployeeIds = scopedEmployees.Select(e => e.Id).ToHashSet();
		var snapshots = (await GetSnapshotsByMonthAsync(internalMonthYear))
			.Where(x => scopedEmployeeIds.Contains(x.EmployeeId))
			.ToList();
		var itemsByEmployee = snapshots
			.Select(snapshot =>
			{
				var item = MapSnapshotToItem(snapshot);
				item.IssueCodes = item.IssueCodes
					.Concat(new[] { IssueCodes.PayrollPolicyConfiguration })
					.Distinct()
					.ToList();
				return item;
			})
			.ToDictionary(x => x.EmployeeId, x => x);

		foreach (var employee in scopedEmployees.Where(x => !itemsByEmployee.ContainsKey(x.Id)))
		{
			itemsByEmployee[employee.Id] = BuildPolicySetupFallbackItem(employee, year, month);
		}

		var existingSlips = await _salarySlipRepository.GetByMonthAsync(internalMonthYear);
		var lockedCount = existingSlips.Count(x =>
			x.Status == SalarySlipStatus.Finalized && scopedEmployeeIds.Contains(x.EmployeeId));

		return new PolicySetupFallbackItems(
			itemsByEmployee.Values.OrderBy(x => x.EmployeeName).ToList(),
			scopedEmployeeIds.Count,
			lockedCount,
			lockedCount > 0);
	}

	private sealed record PolicySetupFallbackItems(
		List<RunPayrollItem> Items,
		int EmployeeCount,
		int LockedCount,
		bool IsGenerated);

	private static RunPayrollItem BuildPolicySetupFallbackItem(Employee employee, int year, int month)
	{
		var daysInMonth = DateTime.DaysInMonth(year, month);
		return new RunPayrollItem
		{
			EmployeeId = employee.Id,
			IsLocked = false,
			ReviewStatus = ReviewStatuses.Pending,
			EmployeeName = FormatEmployeeName(employee),
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

	private async Task<T> WithPayrollSnapshotLockAsync<T>(string monthYear, Func<Task<T>> action)
	{
		var lockKey = $"{CurrentTenant.Id?.ToString("D") ?? "host"}:{monthYear}";
		var snapshotLock = SnapshotRefreshLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
		await snapshotLock.WaitAsync();
		try
		{
			return await action();
		}
		finally
		{
			snapshotLock.Release();
		}
	}

	/// <summary>
	/// Generate and lock payroll for a month
	/// Uses batch processing for better performance with large employee counts
	/// </summary>
	[UnitOfWork]
	public async Task<PayrollLockResponse> LockPayrollAsync(RunPayrollRequest request)
	{
		// Parse and validate month
		var (year, month, internalMonthYear) = ParseMonth(request.Month);
		var academicYearId = await ResolveAcademicYearScopeAsync(request.AcademicYearId, year, month);

		using var scope = _logger.BeginScope(new Dictionary<string, object>
		{
			["Month"] = request.Month,
			["Year"] = year,
			["MonthNum"] = month,
			["OperationType"] = "PayrollLock"
		});

		_logger.LogInformation("Starting payroll lock operation for {Month}", request.Month);

		// Get all payroll-eligible employees in caller scope.
		// For write/finalize flow, unknown-stage employees are denied by default.
		var allScopedEmployees = await GetScopedEmployeesAsync(includeUnknownStageEmployees: false);
		var eligibleEmployees = await GetScopedPayrollEmployeesAsync(year, month, includeUnknownStageEmployees: false);
		await CleanupIneligiblePayrollRowsAsync(internalMonthYear, allScopedEmployees, eligibleEmployees);
		var scopedEmployees = eligibleEmployees
			.Select(e => new PayrollTargetEmployee(e.Id, e.EmployeeCode, e.EmployeeName))
			.ToList();

		if (scopedEmployees.Count == 0)
		{
			_logger.LogWarning("No active employees found for payroll generation in {Month}", request.Month);
			throw new BusinessException(HRErrorCodes.PayrollNoEmployees)
				.WithData("month", request.Month);
		}

		var targetEmployees = ResolvePayrollLockTargetEmployees(
			allScopedEmployees.Select(e => new PayrollTargetEmployee(e.Id, e.EmployeeCode, e.EmployeeName)).ToList(),
			scopedEmployees,
			request.EmployeeIds,
			request.Month);
		var lockCalendar = await _payrollCalendarResolver.ResolveAsync(year, month, academicYearId);

		var existingSlips = await _salarySlipRepository.GetByMonthAsync(internalMonthYear);
		var finalizedEmployeeIds = existingSlips
			.Where(x => x.Status == SalarySlipStatus.Finalized)
			.Select(x => x.EmployeeId)
			.ToHashSet();
		var alreadyFinalizedIds = targetEmployees
			.Where(e => finalizedEmployeeIds.Contains(e.Id))
			.Select(e => e.Id)
			.ToList();
		if (alreadyFinalizedIds.Count > 0)
		{
			throw new BusinessException(HRErrorCodes.PayrollAlreadyLocked)
				.WithData("month", request.Month)
				.WithData("employeeIds", alreadyFinalizedIds);
		}

		var employees = targetEmployees;

		var snapshotByEmployee = (await GetSnapshotsByMonthAsync(internalMonthYear))
			.ToDictionary(x => x.EmployeeId, x => x);
		var unreviewedEmployeeIds = employees
			.Where(e => !snapshotByEmployee.TryGetValue(e.Id, out var snapshot) ||
			            snapshot.IsLocked ||
			            snapshot.ReviewStatus != ReviewStatuses.Reviewed)
			.Select(e => e.Id)
			.ToList();
		if (unreviewedEmployeeIds.Count > 0)
		{
			throw new BusinessException(HRErrorCodes.PayrollReviewRequiredBeforeLock)
				.WithData("month", request.Month)
				.WithData("employeeIds", unreviewedEmployeeIds);
		}

		var blockedBySetupIds = employees
			.Where(e => snapshotByEmployee.TryGetValue(e.Id, out var snapshot) && HasBlockingLockIssue(snapshot.IssueCodesJson))
			.Select(e => e.Id)
			.ToList();
		if (blockedBySetupIds.Count > 0)
		{
			throw new BusinessException(HRErrorCodes.PayrollBlockingIssuesBeforeLock)
				.WithData("month", request.Month)
				.WithData("employeeIds", blockedBySetupIds);
		}

		_logger.LogInformation(
			"Generating salary slips from reviewed payroll snapshots for {ProcessCount} employees",
			employees.Count);

		var generatedCount = 0;
		var skippedCount = 0;
		var failedCount = 0;
		var errors = new List<(string EmployeeCode, string EmployeeName, string Error)>();
		var batchSize = Math.Min(DefaultBatchSize, MaxBatchSize);

		// Process employees in batches with transaction boundary per batch
		for (int i = 0; i < employees.Count; i += batchSize)
		{
			var batch = employees.Skip(i).Take(batchSize).ToList();
			var batchNum = (i / batchSize) + 1;
			var totalBatches = (int)Math.Ceiling((double)employees.Count / batchSize);

			_logger.LogInformation(
				"Processing batch {BatchNum}/{TotalBatches} with {Count} employees",
				batchNum, totalBatches, batch.Count);

			// Process each employee in the batch sequentially (DbContext is not thread-safe)
			foreach (var employee in batch)
			{
				try
				{
					_logger.LogDebug(
						"Processing employee {EmployeeCode} ({EmployeeName})",
						employee.EmployeeCode, employee.EmployeeName);

					var snapshot = snapshotByEmployee[employee.Id];
					var earningLineItems = PayrollSnapshotComponentParser.BuildSalarySlipLineItems(snapshot.EarningComponentsJson);
					var deductionLineItems = PayrollSnapshotComponentParser.BuildSalarySlipLineItems(snapshot.DeductionComponentsJson);
					EnsureSnapshotTotalsMatchLineItems(snapshot, earningLineItems, deductionLineItems);
					var dayCalculationMode = ParseDayCalculationMode(snapshot.DayCalculationMode);

					var salarySlip = await _salarySlipManager.GenerateOrGetSalarySlipFromSnapshotAsync(
						snapshot,
						earningLineItems,
						deductionLineItems,
						dayCalculationMode);

					// Finalize the salary slip if not already finalized
					if (salarySlip.Status == SalarySlipStatus.Finalized)
					{
						_logger.LogDebug(
							"Salary slip for employee {EmployeeCode} is already finalized, skipping",
							employee.EmployeeCode);
						skippedCount++;
						snapshot.MarkLocked();
						await _payrollRunItemSnapshotRepository.UpdateAsync(snapshot, autoSave: false);
					}
					else
					{
						salarySlip.Finalize();
						await _salarySlipRepository.UpdateAsync(salarySlip, autoSave: false);
						generatedCount++;
						snapshot.MarkLocked();
						await _payrollRunItemSnapshotRepository.UpdateAsync(snapshot, autoSave: false);
					}
				}
				catch (BusinessException bex)
				{
					// Business rule violations - log as warning
					_logger.LogWarning(bex,
						"Business rule violation generating salary slip for employee {EmployeeCode} ({EmployeeName}): {ErrorCode}",
						employee.EmployeeCode, employee.EmployeeName, bex.Code);

					failedCount++;
					errors.Add((employee.EmployeeCode ?? "UNKNOWN", employee.EmployeeName ?? "Unknown", bex.Message ?? bex.Code ?? "Unknown error"));
				}
				catch (Exception ex)
				{
					// Unexpected errors - log as error
					_logger.LogError(ex,
						"Unexpected error generating salary slip for employee {EmployeeCode} ({EmployeeName})",
						employee.EmployeeCode, employee.EmployeeName);

					failedCount++;
					errors.Add((employee.EmployeeCode ?? "UNKNOWN", employee.EmployeeName ?? "Unknown", ex.Message));
				}
			}

				// Save changes for this batch (UnitOfWork attribute handles transaction)
				try
				{
					await UnitOfWorkManager.Current!.SaveChangesAsync();
					_logger.LogInformation(
						"Batch {BatchNum}/{TotalBatches} saved successfully. Generated: {Generated}, Skipped: {Skipped}, Failed: {Failed}",
						batchNum, totalBatches, generatedCount, skippedCount, failedCount);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error saving batch {BatchNum}. Rolling back batch changes.", batchNum);
					throw new BusinessException(HRErrorCodes.PayrollBatchSaveFailed)
						.WithData("batchNumber", batchNum)
						.WithData("totalBatches", totalBatches)
						.WithData("month", request.Month);
				}
			}

			// Evaluate final results
			var totalProcessed = generatedCount + skippedCount + failedCount;

			if (generatedCount == 0 && failedCount == employees.Count)
			{
				_logger.LogError(
					"Payroll generation failed for all {Count} employees in {Month}",
					employees.Count, request.Month);

				var firstError = errors.FirstOrDefault();
				throw new BusinessException(HRErrorCodes.PayrollGenerationCompleteFailure)
					.WithData("month", request.Month)
					.WithData("totalEmployees", employees.Count)
					.WithData("firstError", firstError.Error)
					.WithData("firstEmployeeCode", firstError.EmployeeCode);
			}

			_logger.LogInformation(
				"Payroll lock completed for {Month}. Generated: {Generated}, Skipped: {Skipped}, Failed: {Failed}, Total: {Total}",
				request.Month, generatedCount, skippedCount, failedCount, totalProcessed);

			// Build detailed message
			var message = failedCount > 0
				? $"Payroll generated and locked for {generatedCount} employees. {skippedCount} already finalized. {failedCount} failed."
				: skippedCount > 0
					? $"Payroll generated and locked for {generatedCount} employees. {skippedCount} were already finalized."
					: $"Payroll generated and locked successfully for {generatedCount} employees.";

			var response = new PayrollLockResponse
			{
				Message = message,
				Month = request.Month,
				WasAlreadyLocked = false,
				EmployeesProcessed = generatedCount,
				EmployeesSkipped = skippedCount,
				EmployeesFailed = failedCount,
				Errors = failedCount > 0 ? errors.Select(e => $"{e.EmployeeCode}: {e.Error}").ToList() : null
			};
			await _auditWriter.WriteAsync(
				lockCalendar.AcademicYearId,
				"payroll_run",
				GuidGenerator.Create(),
				request.Month,
				"locked",
				$"Locked payroll for {request.Month}.",
				context: new
				{
					RequestedMonth = request.Month,
					Year = year,
					MonthNumber = month,
					employeeCount = employees.Count,
					generatedCount,
					skippedCount,
					failedCount,
					hasPolicyMetadataSnapshots = snapshotByEmployee.Values.Any(x => x.PayrollPolicyId.HasValue || x.AttendancePolicyId.HasValue || x.WorkCalendarPolicyId.HasValue)
				});
			return response;
		}

	public async Task<PayrollLockResponse> MarkReviewedAsync(PayrollRunReviewRequest request)
	{
		var (year, month, internalMonthYear) = ParseMonth(request.Month);
		await ResolveAcademicYearScopeAsync(request.AcademicYearId, year, month);
		var employeeIds = NormalizeReviewEmployeeIds(request.EmployeeIds);
		var actorUserId = CurrentUser.Id ?? throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);

		var targetEmployees = await ResolveScopedTargetEmployeesAsync(year, month, employeeIds);
		await EnsureReviewCanChangeAsync(internalMonthYear, request.Month, targetEmployees);

		var existingByEmployee = await GetReviewsByEmployeeAsync(internalMonthYear);
		var snapshotByEmployee = (await GetSnapshotsByMonthAsync(internalMonthYear))
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
				await _payrollRunReviewRepository.UpdateAsync(existingReview, autoSave: false);
			}
			else
			{
				await _payrollRunReviewRepository.InsertAsync(
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
				await _payrollRunItemSnapshotRepository.UpdateAsync(snapshot, autoSave: false);
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
		var (year, month, internalMonthYear) = ParseMonth(request.Month);
		await ResolveAcademicYearScopeAsync(request.AcademicYearId, year, month);
		var employeeIds = NormalizeReviewEmployeeIds(request.EmployeeIds);
		var targetEmployees = await ResolveScopedTargetEmployeesAsync(year, month, employeeIds);
		await EnsureReviewCanChangeAsync(internalMonthYear, request.Month, targetEmployees);

		var existingByEmployee = await GetReviewsByEmployeeAsync(internalMonthYear);
		var snapshotByEmployee = (await GetSnapshotsByMonthAsync(internalMonthYear))
			.ToDictionary(x => x.EmployeeId, x => x);
		var adjustmentEmployeeIds = (await GetAdjustmentsByEmployeeAsync(
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

			await _payrollRunReviewRepository.DeleteAsync(existingReview, autoSave: false);
			if (snapshotByEmployee.TryGetValue(employee.Id, out var snapshot))
			{
				snapshot.MarkPending();
				await _payrollRunItemSnapshotRepository.UpdateAsync(snapshot, autoSave: false);
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

	public async Task<List<PayrollAdjustmentDto>> GetAdjustmentsAsync(string month, Guid? employeeId = null, Guid? academicYearId = null)
	{
		var (year, monthNumber, internalMonthYear) = ParseMonth(month);
		await ResolveAcademicYearScopeAsync(academicYearId, year, monthNumber);
		var employeeIds = employeeId.HasValue
			? ResolveTargetEmployees(
					await ResolveScopedTargetEmployeesAsync(year, monthNumber, new List<Guid> { employeeId.Value }),
					new List<Guid> { employeeId.Value })
				.Select(x => x.Id)
				.ToList()
			: (await ResolveScopedTargetEmployeesAsync(year, monthNumber, null)).Select(x => x.Id).ToList();

		var adjustments = await GetAdjustmentsByEmployeeAsync(internalMonthYear, employeeIds);
		return adjustments.Values
			.SelectMany(x => x)
			.OrderBy(x => x.EmployeeId)
			.ThenBy(x => x.CreationTime)
			.Select(MapAdjustmentToDto)
			.ToList();
	}

	public async Task<PayrollAdjustmentDto> CreateAdjustmentAsync(PayrollAdjustmentRequest request)
	{
		var (year, month, internalMonthYear) = ParseMonth(request.Month);
		var academicYearId = await ResolveAcademicYearScopeAsync(request.AcademicYearId, year, month);
		var targetEmployees = await ResolveScopedTargetEmployeesAsync(year, month, new List<Guid> { request.EmployeeId });
		await EnsureReviewCanChangeAsync(internalMonthYear, request.Month, targetEmployees);
		await EnsureAdjustmentCanChangeAsync(internalMonthYear, request.Month, request.EmployeeId);

		var adjustment = new PayrollAdjustment(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			internalMonthYear,
			request.EmployeeId,
			request.Name,
			(decimal)request.Amount,
			request.IsEarning,
			request.Reason,
			request.Description);

		await _payrollAdjustmentRepository.InsertAsync(adjustment, autoSave: true);
		await RefreshSnapshotsForEmployeeIdsAsync(
			internalMonthYear,
			year,
			month,
			academicYearId,
			new[] { request.EmployeeId },
			preserveReviewedSnapshots: true);
		return MapAdjustmentToDto(adjustment);
	}

	public async Task<PayrollAdjustmentDto> UpdateAdjustmentAsync(Guid id, PayrollAdjustmentRequest request)
	{
		var (year, month, internalMonthYear) = ParseMonth(request.Month);
		var academicYearId = await ResolveAcademicYearScopeAsync(request.AcademicYearId, year, month);
		var adjustment = await GetAdjustmentOrThrowAsync(id);
		if (adjustment.MonthYear != internalMonthYear || adjustment.EmployeeId != request.EmployeeId)
		{
			throw new BusinessException(HRErrorCodes.PayrollAdjustmentNotFound);
		}

		var targetEmployees = await ResolveScopedTargetEmployeesAsync(year, month, new List<Guid> { request.EmployeeId });
		await EnsureReviewCanChangeAsync(internalMonthYear, request.Month, targetEmployees);
		await EnsureAdjustmentCanChangeAsync(internalMonthYear, request.Month, request.EmployeeId);

		adjustment.Update(
			request.Name,
			(decimal)request.Amount,
			request.IsEarning,
			request.Reason,
			request.Description);
		await _payrollAdjustmentRepository.UpdateAsync(adjustment, autoSave: true);
		await RefreshSnapshotsForEmployeeIdsAsync(
			internalMonthYear,
			year,
			month,
			academicYearId,
			new[] { request.EmployeeId },
			preserveReviewedSnapshots: true);
		return MapAdjustmentToDto(adjustment);
	}

	public async Task DeleteAdjustmentAsync(Guid id)
	{
		var adjustment = await GetAdjustmentOrThrowAsync(id);
		var (year, month, internalMonthYear) = ParseMonth(adjustment.MonthYear);
		var targetEmployees = await ResolveScopedTargetEmployeesAsync(year, month, new List<Guid> { adjustment.EmployeeId });
		await EnsureReviewCanChangeAsync(adjustment.MonthYear, adjustment.MonthYear, targetEmployees);
		await EnsureAdjustmentCanChangeAsync(adjustment.MonthYear, adjustment.MonthYear, adjustment.EmployeeId);
		await _payrollAdjustmentRepository.DeleteAsync(adjustment, autoSave: true);
		await RefreshSnapshotsForEmployeeIdsAsync(
			internalMonthYear,
			year,
			month,
			null,
			new[] { adjustment.EmployeeId },
			preserveReviewedSnapshots: true);
	}

	private async Task EnsureAdjustmentCanChangeAsync(string internalMonthYear, string requestMonth, Guid employeeId)
	{
		var query = await _payrollRunItemSnapshotRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			query = query.Where(x => x.TenantId == CurrentTenant.Id);
		}
		else
		{
			query = query.Where(x => x.TenantId == null);
		}

		var snapshot = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x =>
			x.MonthYear == internalMonthYear &&
			x.EmployeeId == employeeId));

		if (snapshot != null && !snapshot.IsLocked && snapshot.ReviewStatus == ReviewStatuses.Reviewed)
		{
			return;
		}

		throw new BusinessException(HRErrorCodes.PayrollAdjustmentRequiresReviewedPreview)
			.WithData("month", requestMonth)
			.WithData("employeeId", employeeId)
			.WithData("reviewStatus", snapshot?.ReviewStatus ?? ReviewStatuses.Pending)
			.WithData("isLocked", snapshot?.IsLocked ?? false);
	}

	private async Task<List<PayrollRunItemSnapshot>> GetSnapshotsByMonthAsync(string monthYear)
	{
		var query = await _payrollRunItemSnapshotRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			query = query.Where(x => x.TenantId == CurrentTenant.Id);
		}
		else
		{
			query = query.Where(x => x.TenantId == null);
		}

		return await AsyncExecuter.ToListAsync(query.Where(x => x.MonthYear == monthYear));
	}

	private async Task RefreshSnapshotsForEmployeeIdsAsync(
		string internalMonthYear,
		int year,
		int month,
		Guid? academicYearId,
		IReadOnlyCollection<Guid> employeeIds,
		bool preserveReviewedSnapshots)
	{
		var idSet = employeeIds.ToHashSet();
		var scopedEmployees = (await GetScopedPayrollEmployeesAsync(year, month, includeUnknownStageEmployees: false))
			.Where(e => idSet.Contains(e.Id))
			.ToList();

		await RefreshSnapshotsAsync(internalMonthYear, year, month, academicYearId, scopedEmployees, preserveReviewedSnapshots);
	}

	private async Task RefreshSnapshotsAsync(
		string internalMonthYear,
		int year,
		int month,
		Guid? academicYearId,
		List<Employee> scopedEmployees,
		bool preserveReviewedSnapshots)
	{
		if (scopedEmployees.Count == 0)
		{
			return;
		}

		var calendar = await _payrollCalendarResolver.ResolveAsync(year, month, academicYearId);
		var employeeIds = scopedEmployees.Select(e => e.Id).ToList();
		var existingSlips = await _salarySlipRepository.GetByMonthAsync(internalMonthYear);
		var finalizedEmployeeIds = existingSlips
			.Where(x => x.Status == SalarySlipStatus.Finalized)
			.Select(x => x.EmployeeId)
			.ToHashSet();
		var previousMonthNetPayByEmployee = (await _salarySlipRepository.GetByMonthAsync(GetPreviousMonth(internalMonthYear)))
			.Where(x => x.Status == SalarySlipStatus.Finalized)
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(g => g.Key, g => (decimal?)g.OrderByDescending(x => x.ProcessedDate).First().NetSalary);
		var reviewByEmployee = await GetReviewsByEmployeeAsync(internalMonthYear);
		var startDate = DateOnly.FromDateTime(new DateTime(year, month, 1));
		var endDate = DateOnly.FromDateTime(new DateTime(year, month, DateTime.DaysInMonth(year, month)));
		var attendanceByEmployee = await GetAttendanceRecordsAsync(employeeIds, startDate, endDate);
		var leaveUnitsByEmployee = await _payrollApprovedLeaveResolver.ResolveLeaveUnitsByEmployeeAsync(
			employeeIds,
			year,
			month);
		var payrollDetailsByEmployee = await _payrollDetailsRepository.GetByEmployeeIdsAsync(employeeIds);
		var componentsByEmployee = await _payrollComponentRepository.GetByEmployeeIdsAsync(employeeIds);
		var resolvedComponentsByEmployee = await _componentVersionResolver.ResolveByEmployeeAsync(
			componentsByEmployee,
			internalMonthYear);
		var adjustmentsByEmployee = await GetAdjustmentsByEmployeeAsync(internalMonthYear, employeeIds);

		var computedItems = new List<RunPayrollItem>();
		foreach (var employee in scopedEmployees)
		{
			try
			{
				var resolvedPolicies = await _hrPolicyResolver.ResolveAsync(
					employee.Id,
					new DateTime(year, month, 1),
					calendar.AcademicYearId);
				var holidayOverrides = resolvedPolicies.PayrollPolicy.GetHolidayOverrides();
				var workCalendarHolidays = await GetIncludedWorkCalendarHolidaysAsync(resolvedPolicies.PayrollWorkCalendarPolicy.Id, year, month);
				var workingUnitsByDate = PayrollMonthCalendar.BuildWorkingUnitsByDate(
					year,
					month,
					workCalendarHolidays,
					resolvedPolicies.PayrollPolicy.SaturdayWorkPolicy,
					holidayOverrides,
					resolvedPolicies.PayrollWorkCalendarPolicy);
				var payrollPolicyIssueCodes = new List<string>();
				var basis = ResolvePayrollBasisForPreview(
					resolvedPolicies.PayrollPolicy,
					year,
					month,
					workCalendarHolidays,
					workingUnitsByDate,
					payrollPolicyIssueCodes);
				computedItems.Add(BuildPayrollItem(
					employee,
					attendanceByEmployee.GetValueOrDefault(employee.Id) ?? new List<EmployeeAttendance>(),
					payrollDetailsByEmployee.GetValueOrDefault(employee.Id),
					GetResolvedComponents(resolvedComponentsByEmployee, employee.Id, year, month),
					adjustmentsByEmployee.GetValueOrDefault(employee.Id) ?? new List<PayrollAdjustment>(),
					basis,
					year,
					month,
					workCalendarHolidays,
					leaveUnitsByEmployee.GetValueOrDefault(employee.Id),
					resolvedPolicies,
					BuildHolidayImpactJson(year, month, workCalendarHolidays, holidayOverrides),
					finalizedEmployeeIds.Contains(employee.Id),
					reviewByEmployee.GetValueOrDefault(employee.Id),
					previousMonthNetPayByEmployee.GetValueOrDefault(employee.Id),
					payrollPolicyIssueCodes,
					GetEffectiveEmploymentStartDate(employee)
				));
			}
			catch (BusinessException ex) when (IsPayrollPolicySetupException(ex))
			{
				_logger.LogWarning(
					ex,
					"Payroll preview setup issue for employee {EmployeeId} in {Month}. ErrorCode={ErrorCode}",
					employee.Id,
					internalMonthYear,
					ex.Code);
				computedItems.Add(BuildPayrollSetupIssueItem(
					employee,
					attendanceByEmployee.GetValueOrDefault(employee.Id) ?? new List<EmployeeAttendance>(),
					payrollDetailsByEmployee.GetValueOrDefault(employee.Id),
					GetResolvedComponents(resolvedComponentsByEmployee, employee.Id, year, month),
					adjustmentsByEmployee.GetValueOrDefault(employee.Id) ?? new List<PayrollAdjustment>(),
					year,
					month,
					finalizedEmployeeIds.Contains(employee.Id),
					reviewByEmployee.GetValueOrDefault(employee.Id),
					previousMonthNetPayByEmployee.GetValueOrDefault(employee.Id),
					leaveUnitsByEmployee.GetValueOrDefault(employee.Id),
					new[] { IssueCodes.PayrollPolicyConfiguration },
					GetEffectiveEmploymentStartDate(employee)));
			}
		}

		await UpsertSnapshotsAsync(internalMonthYear, computedItems, preserveReviewedSnapshots);
	}

	private static List<EmployeePayrollComponent> GetResolvedComponents(
		ILookup<Guid, EmployeePayrollComponent> resolvedComponentsByEmployee,
		Guid employeeId,
		int year,
		int month) =>
		PayrollComponentMonthScope.FilterForMonth(resolvedComponentsByEmployee[employeeId], year, month);

	private async Task UpsertSnapshotsAsync(
		string monthYear,
		List<RunPayrollItem> items,
		bool preserveReviewedSnapshots = false)
	{
		var existingByEmployee = (await GetSnapshotsByMonthAsync(monthYear))
			.ToDictionary(x => x.EmployeeId, x => x);

		foreach (var item in items)
		{
			var issuesJson = JsonSerializer.Serialize(item.IssueCodes ?? new List<string>());
			var attendancePolicyWarningsJson = JsonSerializer.Serialize(item.AttendancePolicyWarnings ?? new List<string>());
			var earningsJson = JsonSerializer.Serialize(item.EarningComponents ?? new List<RunPayrollComponentItem>());
			var deductionsJson = JsonSerializer.Serialize(item.DeductionComponents ?? new List<RunPayrollComponentItem>());
			var adjustmentsJson = JsonSerializer.Serialize(item.PayrollAdjustments ?? new List<PayrollAdjustmentDto>());

			if (existingByEmployee.TryGetValue(item.EmployeeId, out var existing))
			{
				if (existing.IsLocked)
				{
					continue;
				}

				var shouldResetReview = !preserveReviewedSnapshots &&
					existing.ReviewStatus == ReviewStatuses.Reviewed &&
					SnapshotValuesChanged(existing, item, issuesJson, attendancePolicyWarningsJson, earningsJson, deductionsJson, adjustmentsJson);
				ApplySnapshotValues(existing, item, issuesJson, attendancePolicyWarningsJson, earningsJson, deductionsJson, adjustmentsJson);
				if (shouldResetReview)
				{
					existing.MarkPending();
					await DeleteReviewForEmployeeAsync(monthYear, item.EmployeeId);
				}
				await _payrollRunItemSnapshotRepository.UpdateAsync(existing, autoSave: false);
				continue;
			}

			var snapshot = new PayrollRunItemSnapshot(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				monthYear,
				item.EmployeeId);
			ApplySnapshotValues(snapshot, item, issuesJson, attendancePolicyWarningsJson, earningsJson, deductionsJson, adjustmentsJson);
			await _payrollRunItemSnapshotRepository.InsertAsync(snapshot, autoSave: false);
		}

		await UnitOfWorkManager.Current!.SaveChangesAsync();
	}

	private async Task DeleteReviewForEmployeeAsync(string monthYear, Guid employeeId)
	{
		var query = await _payrollRunReviewRepository.GetQueryableAsync();
		var reviews = await AsyncExecuter.ToListAsync(query.Where(x =>
			x.MonthYear == monthYear &&
			x.EmployeeId == employeeId));
		foreach (var review in reviews)
		{
			await _payrollRunReviewRepository.DeleteAsync(review, autoSave: false);
		}
	}

	private static bool SnapshotValuesChanged(
		PayrollRunItemSnapshot snapshot,
		RunPayrollItem item,
		string issuesJson,
		string attendancePolicyWarningsJson,
		string earningsJson,
		string deductionsJson,
		string adjustmentsJson) =>
		snapshot.BasicSalary != (decimal)item.BasicSalary ||
		snapshot.GrossSalary != (decimal)item.GrossSalary ||
		snapshot.FullGrossSalary != (decimal)item.FullGrossSalary ||
		snapshot.TotalDeduction != (decimal)item.TotalDeduction ||
		snapshot.NetPay != (decimal)item.NetPay ||
		snapshot.PresentDays != (decimal)item.PresentDays ||
		snapshot.HalfWorkingDays != (decimal)item.HalfWorkingDays ||
		snapshot.TotalWorkingDays != (decimal)item.TotalWorkingDays ||
		snapshot.PayableDays != (decimal)item.PayableDays ||
		snapshot.ProrationFactor != (decimal)item.ProrationFactor ||
		snapshot.DayCalculationMode != item.DayCalculationMode ||
		snapshot.PayrollPolicyId != item.PayrollPolicyId ||
		snapshot.PayrollPolicyVersion != item.PayrollPolicyVersion ||
		snapshot.AttendancePolicyId != item.AttendancePolicyId ||
		snapshot.AttendancePolicyVersion != item.AttendancePolicyVersion ||
		snapshot.WorkCalendarPolicyId != item.WorkCalendarPolicyId ||
		snapshot.WorkCalendarPolicyVersion != item.WorkCalendarPolicyVersion ||
		snapshot.BasisDays != (decimal)item.BasisDays ||
		snapshot.PayableUnits != (decimal)item.PayableUnits ||
		snapshot.IssueCodesJson != issuesJson ||
		snapshot.AttendancePolicyWarningsJson != attendancePolicyWarningsJson ||
		snapshot.EarningComponentsJson != earningsJson ||
		snapshot.DeductionComponentsJson != deductionsJson ||
		snapshot.PayrollAdjustmentsJson != adjustmentsJson;

	private static void ApplySnapshotValues(
		PayrollRunItemSnapshot snapshot,
		RunPayrollItem item,
		string issuesJson,
		string attendancePolicyWarningsJson,
		string earningsJson,
		string deductionsJson,
		string adjustmentsJson)
	{
		snapshot.IsLocked = item.IsLocked;
		snapshot.ReviewStatus = item.ReviewStatus;
		snapshot.ReviewedByUserId = item.ReviewedByUserId;
		snapshot.ReviewedAt = item.ReviewedAt;
		snapshot.EmployeeName = item.EmployeeName;
		snapshot.EmployeeIdentifier = item.EmployeeIdentifier;
		snapshot.IsTeaching = item.IsTeaching;
		snapshot.BasicSalary = (decimal)item.BasicSalary;
		snapshot.GrossSalary = (decimal)item.GrossSalary;
		snapshot.FullGrossSalary = (decimal)item.FullGrossSalary;
		snapshot.TotalDeduction = (decimal)item.TotalDeduction;
		snapshot.NetPay = (decimal)item.NetPay;
		snapshot.PreviousMonthNetPay = item.PreviousMonthNetPay.HasValue ? (decimal)item.PreviousMonthNetPay.Value : null;
		snapshot.NetPayDelta = item.NetPayDelta.HasValue ? (decimal)item.NetPayDelta.Value : null;
		snapshot.NetPayDeltaPercent = item.NetPayDeltaPercent.HasValue ? (decimal)item.NetPayDeltaPercent.Value : null;
		snapshot.IssueCodesJson = issuesJson;
		snapshot.PresentDays = (decimal)item.PresentDays;
		snapshot.HalfWorkingDays = (decimal)item.HalfWorkingDays;
		snapshot.TotalWorkingDays = (decimal)item.TotalWorkingDays;
		snapshot.PayableDays = (decimal)item.PayableDays;
		snapshot.ProrationFactor = (decimal)item.ProrationFactor;
		snapshot.DayCalculationMode = item.DayCalculationMode;
		snapshot.UsedDefaultDayCalculation = item.UsedDefaultDayCalculation;
		snapshot.PayrollPolicyId = item.PayrollPolicyId;
		snapshot.PayrollPolicyVersion = item.PayrollPolicyVersion;
		snapshot.AttendancePolicyId = item.AttendancePolicyId;
		snapshot.AttendancePolicyVersion = item.AttendancePolicyVersion;
		snapshot.WorkCalendarPolicyId = item.WorkCalendarPolicyId;
		snapshot.WorkCalendarPolicyVersion = item.WorkCalendarPolicyVersion;
		snapshot.HolidayImpactJson = item.HolidayImpactJson;
		snapshot.BasisDays = (decimal)item.BasisDays;
		snapshot.PayableUnits = (decimal)item.PayableUnits;
		snapshot.PayrollAttendanceSource = item.PayrollAttendanceSource;
		snapshot.AttendancePolicyWarningsJson = attendancePolicyWarningsJson;
		snapshot.EarningComponentsJson = earningsJson;
		snapshot.DeductionComponentsJson = deductionsJson;
		snapshot.PayrollAdjustmentsJson = adjustmentsJson;
	}

	private static RunPayrollItem MapSnapshotToItem(PayrollRunItemSnapshot row)
	{
		return new RunPayrollItem
		{
			EmployeeId = row.EmployeeId,
			IsLocked = row.IsLocked,
			ReviewStatus = row.ReviewStatus,
			ReviewedByUserId = row.ReviewedByUserId,
			ReviewedAt = row.ReviewedAt,
			EmployeeName = row.EmployeeName,
			EmployeeIdentifier = row.EmployeeIdentifier,
			IsTeaching = row.IsTeaching,
			BasicSalary = (double)row.BasicSalary,
			GrossSalary = (double)row.GrossSalary,
			FullGrossSalary = (double)row.FullGrossSalary,
			TotalDeduction = (double)row.TotalDeduction,
			NetPay = (double)row.NetPay,
			PreviousMonthNetPay = row.PreviousMonthNetPay.HasValue ? (double)row.PreviousMonthNetPay.Value : null,
			NetPayDelta = row.NetPayDelta.HasValue ? (double)row.NetPayDelta.Value : null,
			NetPayDeltaPercent = row.NetPayDeltaPercent.HasValue ? (double)row.NetPayDeltaPercent.Value : null,
			IssueCodes = JsonSerializer.Deserialize<List<string>>(row.IssueCodesJson) ?? new List<string>(),
			PresentDays = (double)row.PresentDays,
			HalfWorkingDays = (double)row.HalfWorkingDays,
			TotalWorkingDays = (double)row.TotalWorkingDays,
			PayableDays = (double)row.PayableDays,
			ProrationFactor = (double)row.ProrationFactor,
			DayCalculationMode = row.DayCalculationMode,
			UsedDefaultDayCalculation = row.UsedDefaultDayCalculation,
			PayrollPolicyId = row.PayrollPolicyId,
			PayrollPolicyVersion = row.PayrollPolicyVersion,
			AttendancePolicyId = row.AttendancePolicyId,
			AttendancePolicyVersion = row.AttendancePolicyVersion,
			WorkCalendarPolicyId = row.WorkCalendarPolicyId,
			WorkCalendarPolicyVersion = row.WorkCalendarPolicyVersion,
			HolidayImpactJson = row.HolidayImpactJson,
			BasisDays = (double)row.BasisDays,
			PayableUnits = (double)row.PayableUnits,
			PayrollAttendanceSource = row.PayrollAttendanceSource,
			AttendancePolicyWarnings = JsonSerializer.Deserialize<List<string>>(row.AttendancePolicyWarningsJson)
				?? new List<string>(),
			EarningComponents = JsonSerializer.Deserialize<List<RunPayrollComponentItem>>(row.EarningComponentsJson)
				?? new List<RunPayrollComponentItem>(),
			DeductionComponents = JsonSerializer.Deserialize<List<RunPayrollComponentItem>>(row.DeductionComponentsJson)
				?? new List<RunPayrollComponentItem>(),
			PayrollAdjustments = JsonSerializer.Deserialize<List<PayrollAdjustmentDto>>(row.PayrollAdjustmentsJson)
				?? new List<PayrollAdjustmentDto>()
		};
	}

	private static bool HasAttendancePolicyWarnings(string warningsJson)
	{
		var warnings = JsonSerializer.Deserialize<List<string>>(warningsJson) ?? new List<string>();
		return warnings.Count > 0;
	}

	#region Private Methods

	/// <summary>
	/// Parse month from accepted formats to year, month number, and internal YYYY-MM format
	/// </summary>
	private (int year, int month, string internalFormat) ParseMonth(string monthYear)
	{
		if (string.IsNullOrWhiteSpace(monthYear))
		{
			throw new BusinessException(HRErrorCodes.MonthYearRequired);
		}

		if (!PayrollMonthNormalizer.TryNormalizeToInternal(monthYear, out var internalFormat))
		{
			throw new BusinessException(HRErrorCodes.InvalidMonthYearFormat)
				.WithData("providedMonth", monthYear)
				.WithData("acceptedFormats", "YYYY-MM, MMMM-YYYY, or date-like values");
		}

		var year = int.Parse(internalFormat[..4]);
		var month = int.Parse(internalFormat[5..7]);
		return (year, month, internalFormat);
	}

	private async Task<Guid?> ResolveAcademicYearScopeAsync(Guid? academicYearId, int year, int month)
	{
		if (!academicYearId.HasValue)
		{
			return null;
		}

		var query = await _academicYearRepository.GetQueryableAsync();
		query = query.Where(x => x.Id == academicYearId.Value && x.TenantId == CurrentTenant.Id);
		var academicYear = await AsyncExecuter.FirstOrDefaultAsync(query)
			?? throw new BusinessException(HRErrorCodes.PayrollAcademicYearNotFound)
				.WithData("academicYearId", academicYearId.Value);

		var monthStart = new DateTime(year, month, 1);
		var monthEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month));
		if (academicYear.StartDate > monthEnd || academicYear.EndDate < monthStart)
		{
			throw new BusinessException(HRErrorCodes.PayrollMonthOutsideAcademicYear)
				.WithData("academicYearId", academicYearId.Value)
				.WithData("month", $"{year:D4}-{month:D2}")
				.WithData("academicYearStart", academicYear.StartDate)
				.WithData("academicYearEnd", academicYear.EndDate);
		}

		return academicYear.Id;
	}

	/// <summary>
	/// Batch fetch attendance records for multiple employees
	/// </summary>
	private async Task<Dictionary<Guid, List<EmployeeAttendance>>> GetAttendanceRecordsAsync(
		List<Guid> employeeIds,
		DateOnly startDate,
		DateOnly endDate)
	{
		var attendanceQuery = await _attendanceRepository.GetQueryableAsync();

		if (CurrentTenant.Id.HasValue)
		{
			attendanceQuery = attendanceQuery.Where(a => a.TenantId == CurrentTenant.Id);
		}

		var attendanceRecords = await AsyncExecuter.ToListAsync(
			attendanceQuery
				.Where(a =>
					employeeIds.Contains(a.EmployeeId) &&
					a.AttendanceDate >= startDate &&
					a.AttendanceDate <= endDate)
		);

		return attendanceRecords
			.GroupBy(a => a.EmployeeId)
			.ToDictionary(g => g.Key, g => g.ToList());
	}

	private async Task<bool> HaveSnapshotInvalidationsAsync(
		string monthYear,
		int year,
		int month,
		Guid? academicYearId,
		IReadOnlySet<Guid> employeeIds,
		List<PayrollRunItemSnapshot> snapshots)
	{
		if (employeeIds.Count == 0)
		{
			return false;
		}

		var snapshotByEmployee = snapshots
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(
				x => x.Key,
				x => x.OrderByDescending(row => row.LastModificationTime ?? row.CreationTime).First());
		if (employeeIds.Any(id => !snapshotByEmployee.ContainsKey(id)))
		{
			return true;
		}

		if (await HavePolicyChangesAfterSnapshotsAsync(year, month, academicYearId, snapshotByEmployee))
		{
			return true;
		}

		if (snapshots.Any(PayrollSnapshotComponentParser.HasLegacyComponentSnapshotFormat))
		{
			_logger.LogDebug(
				"Payroll snapshot for {MonthYear} is stale because component metadata is missing.",
				monthYear);
			return true;
		}

		var startDate = DateOnly.FromDateTime(new DateTime(year, month, 1));
		var endDate = DateOnly.FromDateTime(new DateTime(year, month, DateTime.DaysInMonth(year, month)));
		var attendanceQuery = await _attendanceRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			attendanceQuery = attendanceQuery.Where(a => a.TenantId == CurrentTenant.Id);
		}

		var attendanceChanges = await AsyncExecuter.ToListAsync(
			attendanceQuery
				.Where(a =>
					employeeIds.Contains(a.EmployeeId) &&
					a.AttendanceDate >= startDate &&
					a.AttendanceDate <= endDate)
				.Select(a => new
				{
					a.EmployeeId,
					ChangedAt = a.LastModificationTime ?? a.CreationTime
				}));

		var employeesWithAttendance = attendanceChanges.Select(x => x.EmployeeId).ToHashSet();
		foreach (var employeeId in employeesWithAttendance)
		{
			var snapshot = snapshotByEmployee[employeeId];
			if (!snapshot.IsLocked &&
			    (JsonSerializer.Deserialize<List<string>>(snapshot.IssueCodesJson) ?? new List<string>()).Contains(IssueCodes.NoAttendance))
			{
				_logger.LogDebug(
					"Payroll snapshot for employee {EmployeeId} in {MonthYear} is stale because attendance now exists.",
					employeeId,
					monthYear);
				return true;
			}
		}

		foreach (var attendance in attendanceChanges)
		{
			var snapshot = snapshotByEmployee[attendance.EmployeeId];
			if (!snapshot.IsLocked &&
			    attendance.ChangedAt > (snapshot.LastModificationTime ?? snapshot.CreationTime))
			{
				_logger.LogDebug(
					"Payroll snapshot for employee {EmployeeId} in {MonthYear} is stale after attendance change.",
					attendance.EmployeeId,
					monthYear);
				return true;
			}
		}

		return false;
	}

	private async Task<bool> HavePolicyChangesAfterSnapshotsAsync(
		int year,
		int month,
		Guid? academicYearId,
		IReadOnlyDictionary<Guid, PayrollRunItemSnapshot> snapshotByEmployee)
	{
		var unlockedSnapshots = snapshotByEmployee.Values
			.Where(x => !x.IsLocked)
			.ToList();
		if (unlockedSnapshots.Count == 0)
		{
			return false;
		}

		var calendar = await _payrollCalendarResolver.ResolveAsync(year, month, academicYearId);
		var effectiveDate = new DateTime(year, month, 1);
		foreach (var snapshot in unlockedSnapshots)
		{
			HrResolvedPolicies resolvedPolicies;
			try
			{
				resolvedPolicies = await _hrPolicyResolver.ResolveAsync(
					snapshot.EmployeeId,
					effectiveDate,
					calendar.AcademicYearId);
			}
			catch (BusinessException ex) when (IsPayrollPolicySetupException(ex))
			{
				_logger.LogWarning(
					ex,
					"Payroll snapshot for employee {EmployeeId} in {MonthYear} is stale because policy setup cannot be resolved. ErrorCode={ErrorCode}",
					snapshot.EmployeeId,
					snapshot.MonthYear,
					ex.Code);
				return true;
			}

			if (SnapshotPolicyMetadataChanged(snapshot, resolvedPolicies))
			{
				_logger.LogDebug(
					"Payroll snapshot for employee {EmployeeId} in {MonthYear} is stale after policy assignment change.",
					snapshot.EmployeeId,
					snapshot.MonthYear);
				return true;
			}

			if (await SnapshotHasInvalidPayrollBasisAsync(snapshot, resolvedPolicies, year, month))
			{
				_logger.LogDebug(
					"Payroll snapshot for employee {EmployeeId} in {MonthYear} is stale because basis/payable metadata is invalid.",
					snapshot.EmployeeId,
					snapshot.MonthYear);
				return true;
			}
		}

		return false;
	}

	private async Task<bool> SnapshotHasInvalidPayrollBasisAsync(
		PayrollRunItemSnapshot snapshot,
		HrResolvedPolicies resolvedPolicies,
		int year,
		int month)
	{
		var holidayOverrides = resolvedPolicies.PayrollPolicy.GetHolidayOverrides();
		var workCalendarHolidays = await GetIncludedWorkCalendarHolidaysAsync(
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
		var issueCodes = new List<string>();
		var basis = ResolvePayrollBasisForPreview(
			resolvedPolicies.PayrollPolicy,
			year,
			month,
			workCalendarHolidays,
			workingUnitsByDate,
			issueCodes);

		return snapshot.BasisDays != basis.BasisDays ||
		       snapshot.TotalWorkingDays != basis.BasisDays ||
		       snapshot.HolidayImpactJson.Contains("\"paid\"", StringComparison.OrdinalIgnoreCase);
	}

	private static bool SnapshotPolicyMetadataChanged(
		PayrollRunItemSnapshot snapshot,
		HrResolvedPolicies resolvedPolicies) =>
		snapshot.PayrollPolicyId != resolvedPolicies.PayrollPolicy.Id ||
		snapshot.PayrollPolicyVersion != resolvedPolicies.PayrollPolicy.Version ||
		snapshot.AttendancePolicyId != resolvedPolicies.AttendancePolicy.Id ||
		snapshot.AttendancePolicyVersion != resolvedPolicies.AttendancePolicy.Version ||
		snapshot.WorkCalendarPolicyId != resolvedPolicies.PayrollWorkCalendarPolicy.Id ||
		snapshot.WorkCalendarPolicyVersion != resolvedPolicies.PayrollWorkCalendarPolicy.Version;

	/// <summary>
	/// Build payroll item for an employee using pre-loaded data (no DB calls)
	/// </summary>
	private static RunPayrollItem BuildPayrollItem(
		Employee employee,
		List<EmployeeAttendance> attendanceRecords,
		EmployeePayrollDetails? payrollDetails,
		List<EmployeePayrollComponent> components,
		List<PayrollAdjustment> adjustments,
		PayrollBasisDaysResult basis,
		int year,
		int month,
		IReadOnlyList<Holiday> holidays,
		IReadOnlyDictionary<DateOnly, PayrollLeaveUnits>? leaveUnitsByDate,
		HrResolvedPolicies resolvedPolicies,
		string holidayImpactJson,
		bool isLocked,
		PayrollRunReview? review,
		decimal? previousMonthNetPay,
		IReadOnlyList<string>? additionalIssueCodes = null,
		DateOnly? employmentStartDate = null)
	{
		var itemAttendancePolicyWarnings = new List<string>();
		var attendanceSummary = PayrollAttendanceCalculator.CalculatePayableDays(
			attendanceRecords,
			basis,
			year,
			month,
			holidays,
			leaveUnitsByDate,
			employmentStartDate);
		var hasAttendanceOrPaidLeave = HasAttendanceOrPaidLeave(attendanceRecords, leaveUnitsByDate, employmentStartDate, year, month);
		var allComponents = components
			.Concat(adjustments.Select(ToPayrollComponent))
			.ToList();
		var computation = PayrollComputation.Calculate(
			attendanceSummary.PresentDays,
			attendanceSummary.HalfWorkingDays,
			attendanceSummary.PayableDays,
			basis.BasisDays,
			allComponents);
		var netPay = computation.NetPay;
		var netPayDelta = previousMonthNetPay.HasValue ? netPay - previousMonthNetPay.Value : (decimal?)null;
		var netPayDeltaPercent = previousMonthNetPay.HasValue && previousMonthNetPay.Value != 0
			? netPayDelta / previousMonthNetPay.Value * 100m
			: (decimal?)null;

		return new RunPayrollItem
		{
			EmployeeId = employee.Id,
			IsLocked = isLocked,
			ReviewStatus = isLocked ? ReviewStatuses.Locked : review == null ? ReviewStatuses.Pending : ReviewStatuses.Reviewed,
			ReviewedByUserId = review?.ReviewedByUserId,
			ReviewedAt = review?.ReviewedAt,
			EmployeeName = FormatEmployeeName(employee),
			EmployeeIdentifier = employee.EmployeeCode ?? string.Empty,
			IsTeaching = employee.EmployeeGroup == EmployeeGroupType.Teaching,
			BasicSalary = (double)computation.BasicSalary,
			GrossSalary = (double)computation.GrossSalary,
			FullGrossSalary = (double)computation.FullGrossSalary,
			TotalDeduction = (double)computation.TotalDeduction,
			NetPay = (double)netPay,
			PreviousMonthNetPay = previousMonthNetPay.HasValue ? (double)previousMonthNetPay.Value : null,
			NetPayDelta = netPayDelta.HasValue ? (double)netPayDelta.Value : null,
			NetPayDeltaPercent = netPayDeltaPercent.HasValue ? (double)netPayDeltaPercent.Value : null,
			IssueCodes = (additionalIssueCodes ?? Array.Empty<string>())
				.Concat(BuildIssueCodes(payrollDetails, components, basis.BasisDays, computation.PayableDays, netPay, previousMonthNetPay, netPayDeltaPercent, hasAttendanceOrPaidLeave))
				.Distinct()
				.ToList(),
			PresentDays = attendanceSummary.PresentDays,
			HalfWorkingDays = attendanceSummary.HalfWorkingDays,
			TotalWorkingDays = (double)basis.BasisDays,
			PayableDays = (double)computation.PayableDays,
			ProrationFactor = (double)computation.ProrationFactor,
			DayCalculationMode = PayrollDayCalculationApiLabels.ToApiString(basis.EffectiveMode),
			UsedDefaultDayCalculation = basis.UsedTenantDefault,
			PayrollPolicyId = resolvedPolicies.PayrollPolicy.Id,
			PayrollPolicyVersion = resolvedPolicies.PayrollPolicy.Version,
			AttendancePolicyId = resolvedPolicies.AttendancePolicy.Id,
			AttendancePolicyVersion = resolvedPolicies.AttendancePolicy.Version,
			WorkCalendarPolicyId = resolvedPolicies.PayrollWorkCalendarPolicy.Id,
			WorkCalendarPolicyVersion = resolvedPolicies.PayrollWorkCalendarPolicy.Version,
			HolidayImpactJson = holidayImpactJson,
			BasisDays = (double)basis.BasisDays,
			PayableUnits = (double)computation.PayableDays,
			PayrollAttendanceSource = "raw_attendance",
			AttendancePolicyWarnings = itemAttendancePolicyWarnings,
			EarningComponents = computation.Earnings.Select(ToRunPayrollComponentItem).ToList(),
			DeductionComponents = computation.Deductions.Select(ToRunPayrollComponentItem).ToList(),
			PayrollAdjustments = adjustments.Select(MapAdjustmentToDto).ToList()
		};
	}

	private static RunPayrollItem BuildPayrollSetupIssueItem(
		Employee employee,
		List<EmployeeAttendance> attendanceRecords,
		EmployeePayrollDetails? payrollDetails,
		List<EmployeePayrollComponent> components,
		List<PayrollAdjustment> adjustments,
		int year,
		int month,
		bool isLocked,
		PayrollRunReview? review,
		decimal? previousMonthNetPay,
		IReadOnlyDictionary<DateOnly, PayrollLeaveUnits>? leaveUnitsByDate,
		IReadOnlyList<string> issueCodes,
		DateOnly? employmentStartDate = null)
	{
		var basis = new PayrollBasisDaysResult(
			DateTime.DaysInMonth(year, month),
			DayCalculationType.TotalMonthDays,
			UsedTenantDefault: true,
			SaturdayWorkPolicy.FullDay);
		var attendanceSummary = PayrollAttendanceCalculator.CalculatePayableDays(
			attendanceRecords,
			basis,
			year,
			month,
			Array.Empty<Holiday>(),
			leaveUnitsByDate,
			employmentStartDate);
		var hasAttendanceOrPaidLeave = HasAttendanceOrPaidLeave(attendanceRecords, leaveUnitsByDate, employmentStartDate, year, month);
		var allComponents = components
			.Concat(adjustments.Select(ToPayrollComponent))
			.ToList();
		var computation = PayrollComputation.Calculate(
			attendanceSummary.PresentDays,
			attendanceSummary.HalfWorkingDays,
			attendanceSummary.PayableDays,
			basis.BasisDays,
			allComponents);
		var netPay = computation.NetPay;
		var netPayDelta = previousMonthNetPay.HasValue ? netPay - previousMonthNetPay.Value : (decimal?)null;
		var netPayDeltaPercent = previousMonthNetPay.HasValue && previousMonthNetPay.Value != 0
			? netPayDelta / previousMonthNetPay.Value * 100m
			: (decimal?)null;

		return new RunPayrollItem
		{
			EmployeeId = employee.Id,
			IsLocked = isLocked,
			ReviewStatus = isLocked ? ReviewStatuses.Locked : review == null ? ReviewStatuses.Pending : ReviewStatuses.Reviewed,
			ReviewedByUserId = review?.ReviewedByUserId,
			ReviewedAt = review?.ReviewedAt,
			EmployeeName = FormatEmployeeName(employee),
			EmployeeIdentifier = employee.EmployeeCode ?? string.Empty,
			IsTeaching = employee.EmployeeGroup == EmployeeGroupType.Teaching,
			BasicSalary = (double)computation.BasicSalary,
			GrossSalary = (double)computation.GrossSalary,
			FullGrossSalary = (double)computation.FullGrossSalary,
			TotalDeduction = (double)computation.TotalDeduction,
			NetPay = (double)netPay,
			PreviousMonthNetPay = previousMonthNetPay.HasValue ? (double)previousMonthNetPay.Value : null,
			NetPayDelta = netPayDelta.HasValue ? (double)netPayDelta.Value : null,
			NetPayDeltaPercent = netPayDeltaPercent.HasValue ? (double)netPayDeltaPercent.Value : null,
			IssueCodes = issueCodes
				.Concat(BuildIssueCodes(payrollDetails, components, basis.BasisDays, computation.PayableDays, netPay, previousMonthNetPay, netPayDeltaPercent, hasAttendanceOrPaidLeave))
				.Distinct()
				.ToList(),
			PresentDays = attendanceSummary.PresentDays,
			HalfWorkingDays = attendanceSummary.HalfWorkingDays,
			TotalWorkingDays = (double)basis.BasisDays,
			PayableDays = (double)computation.PayableDays,
			ProrationFactor = (double)computation.ProrationFactor,
			DayCalculationMode = PayrollDayCalculationApiLabels.ToApiString(basis.EffectiveMode),
			UsedDefaultDayCalculation = true,
			HolidayImpactJson = "[]",
			BasisDays = (double)basis.BasisDays,
			PayableUnits = (double)computation.PayableDays,
			PayrollAttendanceSource = "raw_attendance",
			EarningComponents = computation.Earnings.Select(ToRunPayrollComponentItem).ToList(),
			DeductionComponents = computation.Deductions.Select(ToRunPayrollComponentItem).ToList(),
			PayrollAdjustments = adjustments.Select(MapAdjustmentToDto).ToList()
		};
	}

	private PayrollBasisDaysResult ResolvePayrollBasisForPreview(
		PayrollPolicy policy,
		int year,
		int month,
		IReadOnlyList<Holiday> holidays,
		IReadOnlyDictionary<DateOnly, decimal> workingUnitsByDate,
		List<string> issueCodes)
	{
		try
		{
			return PayrollBasisDays.ResolvePolicy(
				policy,
				year,
				month,
				workingUnitsByDate.Values.Sum(),
				workingUnitsByDate);
		}
		catch (InvalidOperationException ex) when (
			policy.DayCalculationType == DayCalculationType.ManualDays &&
			ex.Message.StartsWith("ManualDays must be set", StringComparison.Ordinal))
		{
			_logger.LogWarning(
				ex,
				"Payroll policy {PayrollPolicyId} is ManualDays but has no manual basis value for {Year}-{Month:00}. Falling back to calendar days for preview.",
				policy.Id,
				year,
				month);
			issueCodes.Add(IssueCodes.PayrollPolicyConfiguration);
			return new PayrollBasisDaysResult(
				DateTime.DaysInMonth(year, month),
				DayCalculationType.TotalMonthDays,
				UsedTenantDefault: true,
				policy.SaturdayWorkPolicy,
				workingUnitsByDate);
		}
	}

	private static List<PayrollTargetEmployee> ResolveTargetEmployees(
		List<PayrollTargetEmployee> scopedEmployees,
		List<Guid>? requestedEmployeeIds)
	{
		if (requestedEmployeeIds == null || requestedEmployeeIds.Count == 0)
		{
			return scopedEmployees;
		}

		var requestedSet = requestedEmployeeIds.Distinct().ToHashSet();
		var scopedById = scopedEmployees.ToDictionary(e => e.Id, e => e);
		var outOfScopeIds = requestedSet.Where(id => !scopedById.ContainsKey(id)).ToList();
		if (outOfScopeIds.Count > 0)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope)
				.WithData("employeeIds", outOfScopeIds);
		}

		return requestedSet.Select(id => scopedById[id]).ToList();
	}

	private static List<PayrollTargetEmployee> ResolvePayrollLockTargetEmployees(
		List<PayrollTargetEmployee> allScopedEmployees,
		List<PayrollTargetEmployee> eligibleEmployees,
		List<Guid>? requestedEmployeeIds,
		string month)
	{
		if (requestedEmployeeIds == null || requestedEmployeeIds.Count == 0)
		{
			return eligibleEmployees;
		}

		var requestedSet = requestedEmployeeIds.Distinct().ToHashSet();
		var allScopedById = allScopedEmployees.ToDictionary(e => e.Id, e => e);
		var outOfScopeIds = requestedSet.Where(id => !allScopedById.ContainsKey(id)).ToList();
		if (outOfScopeIds.Count > 0)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope)
				.WithData("employeeIds", outOfScopeIds);
		}

		var eligibleById = eligibleEmployees.ToDictionary(e => e.Id, e => e);
		var ineligibleIds = requestedSet.Where(id => !eligibleById.ContainsKey(id)).ToList();
		if (ineligibleIds.Count > 0)
		{
			throw new BusinessException(HRErrorCodes.PayrollNoEmployees)
				.WithData("month", month)
				.WithData("employeeIds", ineligibleIds);
		}

		return requestedSet.Select(id => eligibleById[id]).ToList();
	}

	internal static string BuildHolidayImpactJson(
		int year,
		int month,
		IReadOnlyList<Holiday> holidays,
		IReadOnlyList<PayrollHolidayOverride> holidayOverrides)
	{
		var items = PayrollMonthCalendar.GetResolvedHolidayDaysInMonth(year, month, holidays, holidayOverrides)
			.Select(x => new
			{
				date = x.Date.ToString("yyyy-MM-dd"),
				calendarHolidayId = x.CalendarHolidayId,
				holidayName = x.HolidayName,
				payrollImpact = "excluded",
				x.IsPayrollOnly
			})
			.ToList();

		return JsonSerializer.Serialize(items);
	}

	private async Task<List<Holiday>> GetIncludedWorkCalendarHolidaysAsync(Guid workCalendarPolicyId, int year, int month)
	{
		var start = new DateTime(year, month, 1);
		var end = new DateTime(year, month, DateTime.DaysInMonth(year, month));
		var query = await _workCalendarHolidayRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(query.Where(x =>
			x.WorkCalendarPolicyId == workCalendarPolicyId &&
			x.IsIncluded &&
			x.ToDate >= start &&
			x.FromDate <= end));
		return rows
			.OrderBy(x => x.FromDate)
			.ThenBy(x => x.HolidayName)
			.Select(x => x.ToCalendarHoliday(CurrentTenant.Id))
			.ToList();
	}

	private async Task<List<PayrollTargetEmployee>> ResolveScopedTargetEmployeesAsync(
		int year,
		int month,
		List<Guid>? requestedEmployeeIds)
	{
		var scopedEmployees = (await GetScopedPayrollEmployeesAsync(year, month, includeUnknownStageEmployees: false))
			.Select(e => new PayrollTargetEmployee(e.Id, e.EmployeeCode, e.EmployeeName))
			.ToList();

		return ResolveTargetEmployees(scopedEmployees, requestedEmployeeIds);
	}

	private async Task EnsureReviewCanChangeAsync(
		string internalMonthYear,
		string requestMonth,
		List<PayrollTargetEmployee> targetEmployees)
	{
		var existingSlips = await _salarySlipRepository.GetByMonthAsync(internalMonthYear);
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

	private static RunPayrollComponentItem ToRunPayrollComponentItem(PayrollComputedLineItem item) =>
		new()
		{
			Name = item.Source.Name,
			Amount = (double)item.Amount,
			Description = item.Source.Description,
			IsFixed = item.Source.IsFixed,
			IsTemp = item.Source.IsTemp,
			ComponentType = item.Source.ComponentType.ToString(),
			DependOn = (item.Source.DependOn ?? SalaryComponentDependency.None).ToString()
		};

	private static DayCalculationType ParseDayCalculationMode(string value) => value switch
	{
		"working-days" => DayCalculationType.WorkingDays,
		"total-month-days" => DayCalculationType.TotalMonthDays,
		"manual-days" => DayCalculationType.ManualDays,
		_ => throw new BusinessException(HRErrorCodes.PayrollBlockingIssuesBeforeLock)
			.WithData("reason", "Invalid payroll day calculation mode on reviewed snapshot.")
			.WithData("dayCalculationMode", value)
	};

	private static void EnsureSnapshotTotalsMatchLineItems(
		PayrollRunItemSnapshot snapshot,
		IReadOnlyList<SalarySlipLineItem> earnings,
		IReadOnlyList<SalarySlipLineItem> deductions)
	{
		var totalEarnings = Math.Round(earnings.Sum(x => x.Amount), 2, MidpointRounding.AwayFromZero);
		var totalDeductions = Math.Round(deductions.Sum(x => x.Amount), 2, MidpointRounding.AwayFromZero);
		var netPay = Math.Round(totalEarnings - totalDeductions, 2, MidpointRounding.AwayFromZero);
		if (totalEarnings != Math.Round(snapshot.GrossSalary, 2, MidpointRounding.AwayFromZero) ||
		    totalDeductions != Math.Round(snapshot.TotalDeduction, 2, MidpointRounding.AwayFromZero) ||
		    netPay != Math.Round(snapshot.NetPay, 2, MidpointRounding.AwayFromZero))
		{
			throw new BusinessException(HRErrorCodes.PayrollBlockingIssuesBeforeLock)
				.WithData("employeeId", snapshot.EmployeeId)
				.WithData("month", snapshot.MonthYear)
				.WithData("reason", "Reviewed payroll snapshot totals do not match snapshot line items.");
		}
	}

	private static EmployeePayrollComponent ToPayrollComponent(PayrollAdjustment adjustment) =>
		new(
			adjustment.Id,
			adjustment.EmployeeId,
			adjustment.Name,
			componentId: null,
			adjustment.Description ?? adjustment.Reason,
			adjustment.Amount,
			isFixed: true,
			isTemp: true,
			applicableMonth: DateTime.Parse($"{adjustment.MonthYear}-01"),
			SalaryComponentType.Fixed,
			SalaryComponentDependency.None,
			adjustment.IsEarning,
			usesMasterValue: false);

	private async Task<Dictionary<Guid, List<PayrollAdjustment>>> GetAdjustmentsByEmployeeAsync(
		string monthYear,
		IReadOnlyCollection<Guid> employeeIds)
	{
		if (employeeIds.Count == 0)
		{
			return new Dictionary<Guid, List<PayrollAdjustment>>();
		}

		var query = await _payrollAdjustmentRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			query = query.Where(x => x.TenantId == CurrentTenant.Id);
		}
		else
		{
			query = query.Where(x => x.TenantId == null);
		}

		return (await AsyncExecuter.ToListAsync(query.Where(x =>
				x.MonthYear == monthYear &&
				employeeIds.Contains(x.EmployeeId))))
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(g => g.Key, g => g.OrderBy(x => x.CreationTime).ToList());
	}

	private async Task<PayrollAdjustment> GetAdjustmentOrThrowAsync(Guid id)
	{
		var query = await _payrollAdjustmentRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			query = query.Where(x => x.TenantId == CurrentTenant.Id);
		}
		else
		{
			query = query.Where(x => x.TenantId == null);
		}

		return await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == id))
			?? throw new BusinessException(HRErrorCodes.PayrollAdjustmentNotFound);
	}

	private static PayrollAdjustmentDto MapAdjustmentToDto(PayrollAdjustment adjustment) =>
		new()
		{
			Id = adjustment.Id,
			MonthYear = adjustment.MonthYear,
			EmployeeId = adjustment.EmployeeId,
			Name = adjustment.Name,
			Amount = (double)adjustment.Amount,
			IsEarning = adjustment.IsEarning,
			Reason = adjustment.Reason,
			Description = adjustment.Description,
			CreationTime = adjustment.CreationTime,
			CreatorId = adjustment.CreatorId
		};

	private async Task<Dictionary<Guid, PayrollRunReview>> GetReviewsByEmployeeAsync(string monthYear)
	{
		var query = await _payrollRunReviewRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			query = query.Where(x => x.TenantId == CurrentTenant.Id);
		}
		else
		{
			query = query.Where(x => x.TenantId == null);
		}

		return (await AsyncExecuter.ToListAsync(query.Where(x => x.MonthYear == monthYear)))
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ReviewedAt).First());
	}

	private static string GetPreviousMonth(string internalMonthYear)
	{
		var year = int.Parse(internalMonthYear[..4]);
		var month = int.Parse(internalMonthYear[5..7]);
		return new DateTime(year, month, 1).AddMonths(-1).ToString("yyyy-MM");
	}

	private static List<Guid> NormalizeReviewEmployeeIds(List<Guid>? employeeIds)
	{
		var normalized = employeeIds?
			.Where(id => id != Guid.Empty)
			.Distinct()
			.ToList() ?? new List<Guid>();
		if (normalized.Count == 0)
		{
			throw new BusinessException(HRErrorCodes.PayrollReviewEmployeeIdsRequired);
		}

		return normalized;
	}

	private static List<string> BuildIssueCodes(
		EmployeePayrollDetails? payrollDetails,
		List<EmployeePayrollComponent> components,
		decimal basisDays,
		decimal payableDays,
		decimal netPay,
		decimal? previousMonthNetPay,
		decimal? netPayDeltaPercent,
		bool hasAttendanceOrPaidLeave)
	{
		var issues = new List<string>();

		if (payrollDetails == null || components.Count == 0)
		{
			issues.Add(IssueCodes.MissingSalaryStructure);
		}

		if (basisDays <= 0)
		{
			issues.Add(IssueCodes.NoWorkingDays);
		}

		if (basisDays > 0 && !hasAttendanceOrPaidLeave)
		{
			issues.Add(IssueCodes.NoAttendance);
		}
		else if (basisDays > 0 && payableDays < basisDays)
		{
			issues.Add(IssueCodes.LowAttendance);
		}

		if (netPay < 0)
		{
			issues.Add(IssueCodes.NegativeNetPay);
		}

		if (!previousMonthNetPay.HasValue)
		{
			issues.Add(IssueCodes.NoPreviousMonthComparison);
		}
		else if (netPayDeltaPercent.HasValue && Math.Abs(netPayDeltaPercent.Value) >= 10m)
		{
			issues.Add(IssueCodes.LargeSalaryVariance);
		}

		return issues;
	}

	private static bool HasAttendanceOrPaidLeave(
		IReadOnlyList<EmployeeAttendance> attendanceRecords,
		IReadOnlyDictionary<DateOnly, PayrollLeaveUnits>? leaveUnitsByDate,
		DateOnly? employmentStartDate,
		int year,
		int month)
	{
		var monthStartDate = DateOnly.FromDateTime(new DateTime(year, month, 1));
		var endDate = DateOnly.FromDateTime(new DateTime(year, month, DateTime.DaysInMonth(year, month)));
		var startDate = employmentStartDate.HasValue && employmentStartDate.Value > monthStartDate
			? employmentStartDate.Value
			: monthStartDate;

		if (attendanceRecords.Any(x => x.AttendanceDate >= startDate && x.AttendanceDate <= endDate))
		{
			return true;
		}

		return leaveUnitsByDate?.Any(x =>
			x.Key >= startDate &&
			x.Key <= endDate &&
			x.Value.PaidUnits > 0m) == true;
	}

	private static RunPayrollReadinessSummary BuildReadinessSummary(
		string month,
		List<RunPayrollItem> items,
		bool isGenerated,
		int? employeeCountOverride = null,
		int? lockedCountOverride = null)
	{
		var missingAttendanceCount = items.Count(x => x.IssueCodes.Contains(IssueCodes.NoAttendance));
		var missingSalaryStructureCount = items.Count(x => x.IssueCodes.Contains(IssueCodes.MissingSalaryStructure));
		var policyConfigurationIssueCount = items.Count(x => x.IssueCodes.Contains(IssueCodes.PayrollPolicyConfiguration));
		var reviewedCount = items.Count(x => x.ReviewStatus == ReviewStatuses.Reviewed);
		var lockedCount = lockedCountOverride ?? items.Count(x => x.IsLocked || x.ReviewStatus == ReviewStatuses.Locked);
		var employeeCount = employeeCountOverride ?? items.Count;
		var blockingIssues = new List<string>();
		var recommendedActions = new List<string>();

		if (missingAttendanceCount > 0)
		{
			blockingIssues.Add(IssueCodes.NoAttendance);
			recommendedActions.Add("review-attendance");
			recommendedActions.Add("import-attendance");
		}

		if (missingSalaryStructureCount > 0)
		{
			blockingIssues.Add(IssueCodes.MissingSalaryStructure);
			recommendedActions.Add("assign-salary-structure");
		}

		if (policyConfigurationIssueCount > 0)
		{
			blockingIssues.Add(IssueCodes.PayrollPolicyConfiguration);
			recommendedActions.Add("fix-payroll-policy");
		}

		if (items.Any(x => x.IssueCodes.Contains(IssueCodes.NoWorkingDays)))
		{
			blockingIssues.Add(IssueCodes.NoWorkingDays);
			recommendedActions.Add("review-work-calendar");
		}

		if (items.Any(x => x.IssueCodes.Contains(IssueCodes.NegativeNetPay)))
		{
			blockingIssues.Add(IssueCodes.NegativeNetPay);
			recommendedActions.Add("review-payroll-rows");
		}

		return new RunPayrollReadinessSummary
		{
			Month = month,
			CanPreview = true,
			CanLock = !isGenerated && items.Count > 0 && items.Count == employeeCount && blockingIssues.Count == 0 && reviewedCount == employeeCount,
			EmployeeCount = employeeCount,
			SnapshotCount = items.Count,
			ReviewedCount = reviewedCount,
			LockedCount = lockedCount,
			AttendanceFoundCount = items.Count - missingAttendanceCount,
			MissingAttendanceCount = missingAttendanceCount,
			MissingSalaryStructureCount = missingSalaryStructureCount,
			PolicyConfigurationIssueCount = policyConfigurationIssueCount,
			BlockingIssues = blockingIssues.Distinct().ToList(),
			RecommendedActions = recommendedActions.Distinct().ToList()
		};
	}

	private static bool HasBlockingLockIssue(string issuesJson)
	{
		var issues = JsonSerializer.Deserialize<List<string>>(issuesJson) ?? new List<string>();
		return issues.Contains(IssueCodes.NoAttendance)
		       || issues.Contains(IssueCodes.MissingSalaryStructure)
		       || issues.Contains(IssueCodes.NegativeNetPay)
		       || issues.Contains(IssueCodes.PayrollPolicyConfiguration)
		       || issues.Contains(IssueCodes.NoWorkingDays);
	}

	private static bool IsPayrollPolicySetupException(BusinessException ex) =>
		ex.Code is
			"ShikshaMitra:DefaultPayrollPolicyRequired" or
			"ShikshaMitra:DefaultAttendancePolicyRequired" or
			"ShikshaMitra:AcademicYearRequiredForPolicyResolution" or
			"ShikshaMitra:WorkCalendarPolicyMustBeActive" or
			"ShikshaMitra:PolicyMustBeActive" or
			"ShikshaMitra:InvalidWorkCalendarPolicy";

	private static bool IsSnapshotSchemaMissing(Exception ex)
	{
		var current = ex;
		while (current != null)
		{
			var message = current.Message ?? string.Empty;
			if (
				message.Contains("HR_PayrollRunItems", StringComparison.OrdinalIgnoreCase) &&
				(
					message.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
					message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase) ||
					message.Contains("no such table", StringComparison.OrdinalIgnoreCase)
				))
			{
				return true;
			}

			current = current.InnerException!;
		}

		return false;
	}

	/// <summary>
	/// Format employee name, avoiding double spaces for null/empty middle names
	/// </summary>
	private static string FormatEmployeeName(Employee employee)
	{
		if (!string.IsNullOrWhiteSpace(employee.EmployeeName))
			return employee.EmployeeName;

		var parts = new[] { employee.FirstName, employee.MiddleName, employee.LastName }
			.Where(s => !string.IsNullOrWhiteSpace(s));
		return string.Join(" ", parts);
	}

	#endregion

	private async Task<List<Employee>> GetScopedPayrollEmployeesAsync(
		int year,
		int month,
		bool includeUnknownStageEmployees)
	{
		var monthStart = DateOnly.FromDateTime(new DateTime(year, month, 1));
		var monthEnd = GetPayrollMonthEndDate(year, month);
		var joinedEndExclusive = monthEnd.ToDateTime(TimeOnly.MinValue).AddDays(1);
		var employeeQuery = await GetScopedEmployeeQueryAsync(includeUnknownStageEmployees);
		employeeQuery = employeeQuery.Where(employee =>
			((employee.DateOfJoining.HasValue && employee.DateOfJoining.Value <= monthEnd) ||
			 (!employee.DateOfJoining.HasValue && employee.CreationTime < joinedEndExclusive)) &&
			((!employee.EmployeeStatusType.HasValue) ||
			 employee.EmployeeStatusType == EmployeeStatusType.Active ||
			 (employee.EmployeeStatusType != EmployeeStatusType.Active &&
			  ((employee.StatusEffectiveDate.HasValue &&
			    employee.StatusEffectiveDate.Value >= monthStart) ||
			   (!employee.StatusEffectiveDate.HasValue &&
			    employee.StatusLastChangedAt.HasValue)))));
		var candidates = await AsyncExecuter.ToListAsync(
			employeeQuery
				.OrderBy(e => e.FirstName)
				.ThenBy(e => e.LastName));
		return candidates
			.Where(employee => EmployeeLifecycleEligibility.IsEligibleForPayrollMonth(employee, monthStart, monthEnd))
			.ToList();
	}

	private async Task<List<Employee>> GetScopedEmployeesAsync(bool includeUnknownStageEmployees)
	{
		var employeeQuery = await GetScopedEmployeeQueryAsync(includeUnknownStageEmployees);
		return await AsyncExecuter.ToListAsync(employeeQuery);
	}

	private async Task CleanupIneligiblePayrollRowsAsync(
		string monthYear,
		List<Employee> scopedActiveEmployees,
		List<Employee> eligibleEmployees)
	{
		var ineligibleEmployeeIds = scopedActiveEmployees
			.Select(e => e.Id)
			.Except(eligibleEmployees.Select(e => e.Id))
			.ToHashSet();
		if (ineligibleEmployeeIds.Count == 0)
		{
			return;
		}

		var snapshotQuery = await _payrollRunItemSnapshotRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			snapshotQuery = snapshotQuery.Where(x => x.TenantId == CurrentTenant.Id);
		}
		else
		{
			snapshotQuery = snapshotQuery.Where(x => x.TenantId == null);
		}

		var snapshots = await AsyncExecuter.ToListAsync(snapshotQuery.Where(x =>
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
			_logger.LogInformation(
				"Skipped payroll ineligible-row cleanup for {MonthYear}: {IneligibleEmployeeCount} ineligible employees all have locked snapshots.",
				monthYear,
				ineligibleEmployeeIds.Count);
			return;
		}

		var deletedSnapshotCount = 0;
		foreach (var snapshot in snapshots.Where(x => cleanupEmployeeIds.Contains(x.EmployeeId)))
		{
			await _payrollRunItemSnapshotRepository.DeleteAsync(snapshot, autoSave: false);
			deletedSnapshotCount++;
		}

		var reviewQuery = await _payrollRunReviewRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			reviewQuery = reviewQuery.Where(x => x.TenantId == CurrentTenant.Id);
		}
		else
		{
			reviewQuery = reviewQuery.Where(x => x.TenantId == null);
		}
		var reviews = await AsyncExecuter.ToListAsync(reviewQuery.Where(x =>
			x.MonthYear == monthYear &&
			cleanupEmployeeIds.Contains(x.EmployeeId)));
		var deletedReviewCount = 0;
		foreach (var review in reviews)
		{
			await _payrollRunReviewRepository.DeleteAsync(review, autoSave: false);
			deletedReviewCount++;
		}

		var adjustmentQuery = await _payrollAdjustmentRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			adjustmentQuery = adjustmentQuery.Where(x => x.TenantId == CurrentTenant.Id);
		}
		else
		{
			adjustmentQuery = adjustmentQuery.Where(x => x.TenantId == null);
		}
		var adjustments = await AsyncExecuter.ToListAsync(adjustmentQuery.Where(x =>
			x.MonthYear == monthYear &&
			cleanupEmployeeIds.Contains(x.EmployeeId)));
		var deletedAdjustmentCount = 0;
		foreach (var adjustment in adjustments)
		{
			await _payrollAdjustmentRepository.DeleteAsync(adjustment, autoSave: false);
			deletedAdjustmentCount++;
		}

		await UnitOfWorkManager.Current!.SaveChangesAsync();
		_logger.LogInformation(
			"Cleaned payroll rows for {MonthYear}: {IneligibleEmployeeCount} ineligible employees, {DeletedSnapshotCount} snapshots, {DeletedReviewCount} reviews, {DeletedAdjustmentCount} adjustments deleted.",
			monthYear,
			ineligibleEmployeeIds.Count,
			deletedSnapshotCount,
			deletedReviewCount,
			deletedAdjustmentCount);
	}

	private static DateOnly GetEffectiveEmploymentStartDate(Employee employee) =>
		EmployeeLifecycleEligibility.GetEffectiveEmploymentStartDate(employee);

	private static DateOnly GetPayrollMonthEndDate(int year, int month) =>
		new(year, month, DateTime.DaysInMonth(year, month));

	private async Task<IQueryable<Employee>> GetScopedEmployeeQueryAsync(bool includeUnknownStageEmployees)
	{
		var scope = await EnsureCanOperatePayrollAsync();
		var employeeQuery = await _hrEmployeeScopeQueryService.BuildScopedEmployeeQueryAsync(scope);
		if (includeUnknownStageEmployees || HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return employeeQuery;
		}

		var employeeIds = await AsyncExecuter.ToListAsync(employeeQuery.Select(x => x.Id));
		var stageLookup = await _hrEmployeeScopeQueryService.BuildEmployeeStageLookupAsync(employeeIds);
		var knownStageEmployeeIds = stageLookup
			.Where(x => x.Value.Count > 0)
			.Select(x => x.Key)
			.ToList();
		return employeeQuery.Where(x => knownStageEmployeeIds.Contains(x.Id));
	}

	private async Task<UserScopeContext> EnsureCanOperatePayrollAsync()
	{
		var uid = CurrentUser.Id;
		if (!uid.HasValue)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);
		}

		var scope = await _userScopeService.GetUserScopeAsync(uid.Value);
		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return scope;
		}

		if (!HrUserScopeAccess.HasAnyConcreteHrStageAccess(scope))
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
		}

		return scope;
	}

}

