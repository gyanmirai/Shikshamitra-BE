using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.HR;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.Security.UserScope;
using System;
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
	internal sealed record AttendancePolicyPayrollContext(
		AttendanceConfiguration? Configuration,
		AttendancePolicyEvaluationRun? PublishedRun,
		Dictionary<Guid, List<AttendancePolicyEvaluationResult>> ResultsByEmployee,
		HashSet<Guid> MissingResultEmployeeIds,
		List<string> Warnings);

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
		public const string AttendancePolicyEvaluationMissing = "ATTENDANCE_POLICY_EVALUATION_MISSING";
		public const string AttendancePolicyEvaluationConfigStale = "ATTENDANCE_POLICY_EVALUATION_CONFIG_STALE";
		public const string AttendancePolicyEffectiveBoundary = "ATTENDANCE_POLICY_EFFECTIVE_BOUNDARY";
		public const string AttendancePolicyEvaluationResultMissing = "ATTENDANCE_POLICY_EVALUATION_RESULT_MISSING";
	}

	private readonly IRepository<Employee, Guid> _employeeRepository;
	private readonly IRepository<EmployeeAttendance, Guid> _attendanceRepository;
	private readonly IAttendanceConfigurationRepository _attendanceConfigurationRepository;
	private readonly IRepository<AttendancePolicyEvaluationRun, Guid> _attendancePolicyEvaluationRunRepository;
	private readonly IRepository<AttendancePolicyEvaluationResult, Guid> _attendancePolicyEvaluationResultRepository;
	private readonly IEmployeePayrollDetailsRepository _payrollDetailsRepository;
	private readonly IEmployeePayrollComponentRepository _payrollComponentRepository;
	private readonly ISalarySlipRepository _salarySlipRepository;
	private readonly IRepository<PayrollRunReview, Guid> _payrollRunReviewRepository;
	private readonly IRepository<PayrollRunItemSnapshot, Guid> _payrollRunItemSnapshotRepository;
	private readonly IDayCalculationSettingRepository _dayCalculationSettingRepository;
	private readonly PayrollCalendarResolver _payrollCalendarResolver;
	private readonly PayrollApprovedLeaveResolver _payrollApprovedLeaveResolver;
	private readonly SalarySlipManager _salarySlipManager;
	private readonly PayrollComponentVersionResolver _componentVersionResolver;
	private readonly IUserScopeService _userScopeService;
	private readonly HrUserScopeAccessService _hrScopeAccessService;
	private readonly ILogger<RunPayrollAppService> _logger;

	// Configuration: Batch size for processing employees
	private const int DefaultBatchSize = 50;
	private const int MaxBatchSize = 100;

	public RunPayrollAppService(
		IRepository<Employee, Guid> employeeRepository,
		IRepository<EmployeeAttendance, Guid> attendanceRepository,
		IAttendanceConfigurationRepository attendanceConfigurationRepository,
		IRepository<AttendancePolicyEvaluationRun, Guid> attendancePolicyEvaluationRunRepository,
		IRepository<AttendancePolicyEvaluationResult, Guid> attendancePolicyEvaluationResultRepository,
		IEmployeePayrollDetailsRepository payrollDetailsRepository,
		IEmployeePayrollComponentRepository payrollComponentRepository,
		ISalarySlipRepository salarySlipRepository,
		IRepository<PayrollRunReview, Guid> payrollRunReviewRepository,
		IRepository<PayrollRunItemSnapshot, Guid> payrollRunItemSnapshotRepository,
		IDayCalculationSettingRepository dayCalculationSettingRepository,
		PayrollCalendarResolver payrollCalendarResolver,
		PayrollApprovedLeaveResolver payrollApprovedLeaveResolver,
		SalarySlipManager salarySlipManager,
		PayrollComponentVersionResolver componentVersionResolver,
		IUserScopeService userScopeService,
		HrUserScopeAccessService hrScopeAccessService,
		ILogger<RunPayrollAppService> logger)
	{
		_employeeRepository = employeeRepository;
		_attendanceRepository = attendanceRepository;
		_attendanceConfigurationRepository = attendanceConfigurationRepository;
		_attendancePolicyEvaluationRunRepository = attendancePolicyEvaluationRunRepository;
		_attendancePolicyEvaluationResultRepository = attendancePolicyEvaluationResultRepository;
		_payrollDetailsRepository = payrollDetailsRepository;
		_payrollComponentRepository = payrollComponentRepository;
		_salarySlipRepository = salarySlipRepository;
		_payrollRunReviewRepository = payrollRunReviewRepository;
		_payrollRunItemSnapshotRepository = payrollRunItemSnapshotRepository;
		_dayCalculationSettingRepository = dayCalculationSettingRepository;
		_payrollCalendarResolver = payrollCalendarResolver;
		_payrollApprovedLeaveResolver = payrollApprovedLeaveResolver;
		_salarySlipManager = salarySlipManager;
		_componentVersionResolver = componentVersionResolver;
		_userScopeService = userScopeService;
		_hrScopeAccessService = hrScopeAccessService;
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

			_logger.LogInformation(
				"Generating payroll preview for {Month} (Year: {Year}, Month: {MonthNum}, Page: {Page}, PageSize: {PageSize})",
				input.Month, year, month, input.Page, input.PageSize);

			// Get all active employees in caller scope.
			var employeeQuery = await GetScopedActiveEmployeeQueryAsync(includeUnknownStageEmployees: false);
			var scopedEmployees = await AsyncExecuter.ToListAsync(
				employeeQuery
					.OrderBy(e => e.FirstName)
					.ThenBy(e => e.LastName));
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
			var calendar = await _payrollCalendarResolver.ResolveAsync(year, month);
			var employeeIds = scopedEmployees.Select(e => e.Id).ToList();
			var attendancePolicyContext = await GetAttendancePolicyPayrollContextAsync(
				calendar.AcademicYearId,
				year,
				month,
				internalMonthYear,
				employeeIds);
			var attendancePolicySnapshotsStale = AreAttendancePolicySnapshotsStale(
				inScopeSnapshots,
				attendancePolicyContext);

			if (input.ForceRefresh || inScopeSnapshots.Count == 0 || attendancePolicySnapshotsStale)
			{
				var previousMonthNetPayByEmployee = (await _salarySlipRepository.GetByMonthAsync(GetPreviousMonth(internalMonthYear)))
					.Where(x => x.Status == SalarySlipStatus.Finalized)
					.GroupBy(x => x.EmployeeId)
					.ToDictionary(g => g.Key, g => (decimal?)g.OrderByDescending(x => x.ProcessedDate).First().NetSalary);
				var reviewByEmployee = await GetReviewsByEmployeeAsync(internalMonthYear);
				var daySetting = await _dayCalculationSettingRepository.FindEffectiveAsync(CurrentTenant.Id, calendar.AcademicYearId);
				var saturdayWorkPolicy = daySetting?.GetEffectiveSaturdayWorkPolicyForMonth(year, month) ?? SaturdayWorkPolicy.FullDay;
				var holidayOverrides = daySetting?.GetEffectiveHolidayOverridesForMonth(year, month) ?? Array.Empty<PayrollHolidayOverride>();
				var workingUnitsByDate = PayrollMonthCalendar.BuildWorkingUnitsByDate(
					year,
					month,
					calendar.Holidays,
					saturdayWorkPolicy,
					holidayOverrides);
				var basis = PayrollBasisDays.Resolve(daySetting, year, month, workingUnitsByDate.Values.Sum(), workingUnitsByDate);
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

				var computedItems = new List<RunPayrollItem>();
				foreach (var employee in scopedEmployees)
				{
					computedItems.Add(BuildPayrollItem(
						employee,
						attendanceByEmployee.GetValueOrDefault(employee.Id) ?? new List<EmployeeAttendance>(),
						payrollDetailsByEmployee.GetValueOrDefault(employee.Id),
						resolvedComponentsByEmployee[employee.Id].ToList(),
						basis,
						year,
						month,
						calendar.Holidays,
						leaveUnitsByEmployee.GetValueOrDefault(employee.Id),
						attendancePolicyContext.PublishedRun,
						attendancePolicyContext.ResultsByEmployee.GetValueOrDefault(employee.Id),
						attendancePolicyContext.Warnings,
						finalizedEmployeeIds.Contains(employee.Id),
						reviewByEmployee.GetValueOrDefault(employee.Id),
						previousMonthNetPayByEmployee.GetValueOrDefault(employee.Id)
					));
				}

				await UpsertSnapshotsAsync(internalMonthYear, computedItems);
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

			return new RunPayrollResponse
			{
				IsGenerated = isGenerated,
				TotalCount = totalCount,
				CurrentPage = input.Page,
				PageSize = input.PageSize,
				TotalPages = (int)Math.Ceiling((double)totalCount / input.PageSize),
				Items = items
			};
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

	/// <summary>
	/// Generate and lock payroll for a month
	/// Uses batch processing for better performance with large employee counts
	/// </summary>
	[UnitOfWork]
	public async Task<PayrollLockResponse> LockPayrollAsync(RunPayrollRequest request)
	{
		// Parse and validate month
		var (year, month, internalMonthYear) = ParseMonth(request.Month);

		using var scope = _logger.BeginScope(new Dictionary<string, object>
		{
			["Month"] = request.Month,
			["Year"] = year,
			["MonthNum"] = month,
			["OperationType"] = "PayrollLock"
		});

		_logger.LogInformation("Starting payroll lock operation for {Month}", request.Month);

		// Get all active employees in caller scope.
		// For write/finalize flow, unknown-stage employees are denied by default.
		var employeeQuery = await GetScopedActiveEmployeeQueryAsync(includeUnknownStageEmployees: false);
		var scopedEmployees = await AsyncExecuter.ToListAsync(
			employeeQuery
				.Select(e => new PayrollTargetEmployee(e.Id, e.EmployeeCode, e.EmployeeName))
		);

		if (scopedEmployees.Count == 0)
		{
			_logger.LogWarning("No active employees found for payroll generation in {Month}", request.Month);
			throw new BusinessException(HRErrorCodes.PayrollNoEmployees)
				.WithData("month", request.Month);
		}

		var targetEmployees = ResolveTargetEmployees(scopedEmployees, request.EmployeeIds);
		var lockCalendar = await _payrollCalendarResolver.ResolveAsync(year, month);
		var lockAttendancePolicyContext = await GetAttendancePolicyPayrollContextAsync(
			lockCalendar.AcademicYearId,
			year,
			month,
			internalMonthYear,
			targetEmployees.Select(x => x.Id).ToList());
		if (lockAttendancePolicyContext.Configuration?.PayrollIntegrationMode == AttendancePayrollIntegrationMode.ApprovedResults &&
			lockAttendancePolicyContext.Configuration.RequirePublishedAttendanceForPayrollLock &&
			lockAttendancePolicyContext.PublishedRun == null)
		{
			throw new BusinessException("ShikshaMitra:PublishedAttendanceEvaluationRequired")
				.WithData("month", request.Month);
		}
		if (lockAttendancePolicyContext.Configuration?.PayrollIntegrationMode == AttendancePayrollIntegrationMode.ApprovedResults &&
			lockAttendancePolicyContext.Configuration.RequirePublishedAttendanceForPayrollLock &&
			lockAttendancePolicyContext.MissingResultEmployeeIds.Count > 0)
		{
			throw new BusinessException("ShikshaMitra:PublishedAttendanceEvaluationResultsIncomplete")
				.WithData("month", request.Month)
				.WithData("employeeIds", lockAttendancePolicyContext.MissingResultEmployeeIds);
		}

		var existingSlips = await _salarySlipRepository.GetByMonthAsync(internalMonthYear);
		var finalizedEmployeeIds = existingSlips
			.Where(x => x.Status == SalarySlipStatus.Finalized)
			.Select(x => x.EmployeeId)
			.ToHashSet();
		var employees = targetEmployees
			.Where(e => !finalizedEmployeeIds.Contains(e.Id))
			.ToList();
		var alreadyFinalizedCount = targetEmployees.Count - employees.Count;

		var snapshotByEmployee = (await GetSnapshotsByMonthAsync(internalMonthYear))
			.ToDictionary(x => x.EmployeeId, x => x);
		var reviewedEmployeeIds = snapshotByEmployee.Values
			.Where(x => x.ReviewStatus == ReviewStatuses.Reviewed || x.IsLocked)
			.Select(x => x.EmployeeId)
			.ToHashSet();
		if (reviewedEmployeeIds.Count == 0)
		{
			reviewedEmployeeIds = (await GetReviewsByEmployeeAsync(internalMonthYear)).Keys.ToHashSet();
		}
		var unreviewedEmployeeIds = employees
			.Where(e => !reviewedEmployeeIds.Contains(e.Id))
			.Select(e => e.Id)
			.ToList();
		if (unreviewedEmployeeIds.Count > 0)
		{
			throw new BusinessException(HRErrorCodes.PayrollReviewRequiredBeforeLock)
				.WithData("month", request.Month)
				.WithData("employeeIds", unreviewedEmployeeIds);
		}

		if (employees.Count == 0)
		{
			_logger.LogInformation(
				"Payroll for {Month} is already locked for the targeted employee set. Returning idempotent response.",
				request.Month);

			return new PayrollLockResponse
			{
				Message = $"Payroll for {request.Month} is already generated and locked for the targeted employees.",
				Month = request.Month,
				WasAlreadyLocked = true,
				EmployeesProcessed = 0,
				EmployeesSkipped = alreadyFinalizedCount
			};
		}

		_logger.LogInformation(
			"Generating salary slips for {ProcessCount} employees ({SkippedCount} already finalized)",
			employees.Count,
			alreadyFinalizedCount);

		var generatedCount = 0;
		var skippedCount = alreadyFinalizedCount;
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

					// Generate or get existing salary slip
					var salarySlip = await _salarySlipManager.GenerateOrGetSalarySlipAsync(
						employee.Id,
						internalMonthYear,
						forceRegenerate: true);

					// Finalize the salary slip if not already finalized
					if (salarySlip.Status == SalarySlipStatus.Finalized)
					{
						_logger.LogDebug(
							"Salary slip for employee {EmployeeCode} is already finalized, skipping",
							employee.EmployeeCode);
						skippedCount++;
						if (snapshotByEmployee.TryGetValue(employee.Id, out var snapshot))
						{
							snapshot.MarkLocked();
							await _payrollRunItemSnapshotRepository.UpdateAsync(snapshot, autoSave: false);
						}
					}
					else
					{
						salarySlip.Finalize();
						await _salarySlipRepository.UpdateAsync(salarySlip, autoSave: false);
						generatedCount++;
						if (snapshotByEmployee.TryGetValue(employee.Id, out var snapshot))
						{
							snapshot.MarkLocked();
							await _payrollRunItemSnapshotRepository.UpdateAsync(snapshot, autoSave: false);
						}
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

			return new PayrollLockResponse
			{
				Message = message,
				Month = request.Month,
				WasAlreadyLocked = false,
				EmployeesProcessed = generatedCount,
				EmployeesSkipped = skippedCount,
				EmployeesFailed = failedCount,
				Errors = failedCount > 0 ? errors.Select(e => $"{e.EmployeeCode}: {e.Error}").ToList() : null
			};
		}

	public async Task<PayrollLockResponse> MarkReviewedAsync(PayrollRunReviewRequest request)
	{
		var (_, _, internalMonthYear) = ParseMonth(request.Month);
		var employeeIds = NormalizeReviewEmployeeIds(request.EmployeeIds);
		var actorUserId = CurrentUser.Id ?? throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);

		var targetEmployees = await ResolveScopedTargetEmployeesAsync(employeeIds);
		await EnsureReviewCanChangeAsync(internalMonthYear, request.Month, targetEmployees);

		var existingByEmployee = await GetReviewsByEmployeeAsync(internalMonthYear);
		var snapshotByEmployee = (await GetSnapshotsByMonthAsync(internalMonthYear))
			.ToDictionary(x => x.EmployeeId, x => x);
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
		var (_, _, internalMonthYear) = ParseMonth(request.Month);
		var employeeIds = NormalizeReviewEmployeeIds(request.EmployeeIds);
		var targetEmployees = await ResolveScopedTargetEmployeesAsync(employeeIds);
		await EnsureReviewCanChangeAsync(internalMonthYear, request.Month, targetEmployees);

		var existingByEmployee = await GetReviewsByEmployeeAsync(internalMonthYear);
		var snapshotByEmployee = (await GetSnapshotsByMonthAsync(internalMonthYear))
			.ToDictionary(x => x.EmployeeId, x => x);
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

	private async Task UpsertSnapshotsAsync(string monthYear, List<RunPayrollItem> items)
	{
		var existingByEmployee = (await GetSnapshotsByMonthAsync(monthYear))
			.ToDictionary(x => x.EmployeeId, x => x);

		foreach (var item in items)
		{
			var issuesJson = JsonSerializer.Serialize(item.IssueCodes ?? new List<string>());
			var attendancePolicyWarningsJson = JsonSerializer.Serialize(item.AttendancePolicyWarnings ?? new List<string>());
			var earningsJson = JsonSerializer.Serialize(item.EarningComponents ?? new List<RunPayrollComponentItem>());
			var deductionsJson = JsonSerializer.Serialize(item.DeductionComponents ?? new List<RunPayrollComponentItem>());

			if (existingByEmployee.TryGetValue(item.EmployeeId, out var existing))
			{
				ApplySnapshotValues(existing, item, issuesJson, attendancePolicyWarningsJson, earningsJson, deductionsJson);
				await _payrollRunItemSnapshotRepository.UpdateAsync(existing, autoSave: false);
				continue;
			}

			var snapshot = new PayrollRunItemSnapshot(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				monthYear,
				item.EmployeeId);
			ApplySnapshotValues(snapshot, item, issuesJson, attendancePolicyWarningsJson, earningsJson, deductionsJson);
			await _payrollRunItemSnapshotRepository.InsertAsync(snapshot, autoSave: false);
		}

		await UnitOfWorkManager.Current!.SaveChangesAsync();
	}

	private static void ApplySnapshotValues(
		PayrollRunItemSnapshot snapshot,
		RunPayrollItem item,
		string issuesJson,
		string attendancePolicyWarningsJson,
		string earningsJson,
		string deductionsJson)
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
		snapshot.AttendancePolicyEvaluationRunId = item.AttendancePolicyEvaluationRunId;
		snapshot.PayrollAttendanceSource = item.PayrollAttendanceSource;
		snapshot.AttendancePolicyWarningsJson = attendancePolicyWarningsJson;
		snapshot.EarningComponentsJson = earningsJson;
		snapshot.DeductionComponentsJson = deductionsJson;
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
			AttendancePolicyEvaluationRunId = row.AttendancePolicyEvaluationRunId,
			PayrollAttendanceSource = row.PayrollAttendanceSource,
			AttendancePolicyWarnings = JsonSerializer.Deserialize<List<string>>(row.AttendancePolicyWarningsJson)
				?? new List<string>(),
			EarningComponents = JsonSerializer.Deserialize<List<RunPayrollComponentItem>>(row.EarningComponentsJson)
				?? new List<RunPayrollComponentItem>(),
			DeductionComponents = JsonSerializer.Deserialize<List<RunPayrollComponentItem>>(row.DeductionComponentsJson)
				?? new List<RunPayrollComponentItem>()
		};
	}

	internal static bool AreAttendancePolicySnapshotsStale(
		List<PayrollRunItemSnapshot> snapshots,
		AttendancePolicyPayrollContext context)
	{
		if (snapshots.Count == 0)
		{
			return false;
		}

		if (context.Configuration?.PayrollIntegrationMode != AttendancePayrollIntegrationMode.ApprovedResults)
		{
			return snapshots.Any(x =>
				x.AttendancePolicyEvaluationRunId.HasValue ||
				!string.Equals(x.PayrollAttendanceSource, "raw_attendance", StringComparison.OrdinalIgnoreCase) ||
				HasAttendancePolicyWarnings(x.AttendancePolicyWarningsJson));
		}

		foreach (var snapshot in snapshots)
		{
			if (snapshot.AttendancePolicyEvaluationRunId != context.PublishedRun?.Id)
			{
				return true;
			}

			var hasEmployeeResults = context.ResultsByEmployee.ContainsKey(snapshot.EmployeeId);
			var expectedSource = context.PublishedRun != null && hasEmployeeResults
				? "published_attendance_policy"
				: "raw_attendance";
			if (!string.Equals(snapshot.PayrollAttendanceSource, expectedSource, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			var snapshotWarnings = JsonSerializer.Deserialize<List<string>>(snapshot.AttendancePolicyWarningsJson)
				?? new List<string>();
			var expectedWarnings = BuildExpectedAttendancePolicyWarnings(context, hasEmployeeResults);
			if (!WarningSetsEqual(snapshotWarnings, expectedWarnings))
			{
				return true;
			}
		}

		return false;
	}

	private static List<string> BuildExpectedAttendancePolicyWarnings(
		AttendancePolicyPayrollContext context,
		bool hasEmployeeResults)
	{
		var warnings = context.Warnings.ToList();
		if (context.PublishedRun != null && !hasEmployeeResults)
		{
			warnings.Add("Published attendance evaluation exists, but this employee has no evaluation results.");
		}

		return warnings;
	}

	private static bool WarningSetsEqual(IReadOnlyCollection<string> current, IReadOnlyCollection<string> expected)
	{
		var currentSet = NormalizeWarnings(current);
		var expectedSet = NormalizeWarnings(expected);
		return currentSet.SetEquals(expectedSet);
	}

	private static HashSet<string> NormalizeWarnings(IEnumerable<string> warnings) =>
		warnings
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Select(x => x.Trim())
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

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

	private async Task<AttendancePolicyPayrollContext> GetAttendancePolicyPayrollContextAsync(
		Guid? academicYearId,
		int year,
		int month,
		string internalMonthYear,
		IReadOnlyCollection<Guid> targetEmployeeIds)
	{
		if (!academicYearId.HasValue)
		{
			return new AttendancePolicyPayrollContext(null, null, new Dictionary<Guid, List<AttendancePolicyEvaluationResult>>(), new HashSet<Guid>(), new List<string>());
		}

		var configuration = await _attendanceConfigurationRepository.FindByAcademicYearIdAsync(academicYearId.Value);
		var warnings = new List<string>();
		if (configuration == null || configuration.PayrollIntegrationMode != AttendancePayrollIntegrationMode.ApprovedResults)
		{
			return new AttendancePolicyPayrollContext(configuration, null, new Dictionary<Guid, List<AttendancePolicyEvaluationResult>>(), new HashSet<Guid>(), warnings);
		}

		var runQuery = await _attendancePolicyEvaluationRunRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			runQuery = runQuery.Where(x => x.TenantId == CurrentTenant.Id);
		}
		else
		{
			runQuery = runQuery.Where(x => x.TenantId == null);
		}

		var publishedRun = (await AsyncExecuter.ToListAsync(
				runQuery.Where(x =>
					x.AcademicYearId == academicYearId.Value &&
					x.Year == year &&
					x.Month == month &&
					x.Status == AttendancePolicyEvaluationRunStatus.Published)
					.OrderByDescending(x => x.PublishedAt ?? x.CreationTime)))
			.FirstOrDefault();

		if (publishedRun == null)
		{
			warnings.Add("Active attendance policy is configured for approved payroll results, but no published evaluation exists for this payroll month.");
			return new AttendancePolicyPayrollContext(configuration, null, new Dictionary<Guid, List<AttendancePolicyEvaluationResult>>(), new HashSet<Guid>(), warnings);
		}

		var activeHash = AttendanceConfigurationHasher.Compute(configuration);
		if (!string.Equals(activeHash, publishedRun.ConfigHash, StringComparison.OrdinalIgnoreCase))
		{
			warnings.Add("Published attendance evaluation was created from an older attendance configuration.");
		}

		var monthStart = new DateTime(year, month, 1);
		var monthEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month));
		if ((configuration.EffectiveFrom.HasValue && configuration.EffectiveFrom.Value.Date > monthStart) ||
			(configuration.EffectiveTo.HasValue && configuration.EffectiveTo.Value.Date < monthEnd))
		{
			warnings.Add("Payroll month overlaps attendance policy effective boundaries.");
		}

		var resultQuery = await _attendancePolicyEvaluationResultRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			resultQuery = resultQuery.Where(x => x.TenantId == CurrentTenant.Id);
		}
		else
		{
			resultQuery = resultQuery.Where(x => x.TenantId == null);
		}

		var results = await AsyncExecuter.ToListAsync(
			resultQuery.Where(x =>
				x.AttendancePolicyEvaluationRunId == publishedRun.Id &&
				targetEmployeeIds.Contains(x.EmployeeId)));
		var resultsByEmployee = results.GroupBy(x => x.EmployeeId).ToDictionary(x => x.Key, x => x.ToList());
		var missingResultEmployeeIds = targetEmployeeIds
			.Where(employeeId => !resultsByEmployee.ContainsKey(employeeId))
			.ToHashSet();
		return new AttendancePolicyPayrollContext(
			configuration,
			publishedRun,
			resultsByEmployee,
			missingResultEmployeeIds,
			warnings);
	}

	/// <summary>
	/// Build payroll item for an employee using pre-loaded data (no DB calls)
	/// </summary>
	private static RunPayrollItem BuildPayrollItem(
		Employee employee,
		List<EmployeeAttendance> attendanceRecords,
		EmployeePayrollDetails? payrollDetails,
		List<EmployeePayrollComponent> components,
		PayrollBasisDaysResult basis,
		int year,
		int month,
		IReadOnlyList<Holiday> holidays,
		IReadOnlyDictionary<DateOnly, PayrollLeaveUnits>? leaveUnitsByDate,
		AttendancePolicyEvaluationRun? attendancePolicyRun,
		List<AttendancePolicyEvaluationResult>? attendancePolicyResults,
		List<string> attendancePolicyWarnings,
		bool isLocked,
		PayrollRunReview? review,
		decimal? previousMonthNetPay)
	{
		var hasPublishedEvaluation = attendancePolicyRun != null && attendancePolicyResults is { Count: > 0 };
		var itemAttendancePolicyWarnings = attendancePolicyWarnings.ToList();
		if (attendancePolicyRun != null && !hasPublishedEvaluation)
		{
			itemAttendancePolicyWarnings.Add("Published attendance evaluation exists, but this employee has no evaluation results.");
		}
		var attendanceSummary = hasPublishedEvaluation
			? PayrollAttendanceCalculator.CalculatePayableDays(
				attendancePolicyResults!
					.Select(x => new PayrollApprovedAttendanceDay(x.AttendanceDate, x.EvaluatedStatus, x.EvaluatedPayableUnits))
					.ToList(),
				basis,
				year,
				month,
				holidays,
				leaveUnitsByDate)
			: PayrollAttendanceCalculator.CalculatePayableDays(
				attendanceRecords,
				basis,
				year,
				month,
				holidays,
				leaveUnitsByDate);
		var computation = PayrollComputation.Calculate(
			attendanceSummary.PresentDays,
			attendanceSummary.HalfWorkingDays,
			attendanceSummary.PayableDays,
			basis.BasisDays,
			components);
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
			IssueCodes = BuildIssueCodes(payrollDetails, components, basis.BasisDays, computation.PayableDays, netPay, previousMonthNetPay, netPayDeltaPercent)
				.Concat(itemAttendancePolicyWarnings.Select(ToAttendancePolicyIssueCode))
				.Distinct()
				.ToList(),
			PresentDays = attendanceSummary.PresentDays,
			HalfWorkingDays = attendanceSummary.HalfWorkingDays,
			TotalWorkingDays = (double)basis.BasisDays,
			PayableDays = (double)computation.PayableDays,
			ProrationFactor = (double)computation.ProrationFactor,
			DayCalculationMode = PayrollDayCalculationApiLabels.ToApiString(basis.EffectiveMode),
			UsedDefaultDayCalculation = basis.UsedTenantDefault,
			AttendancePolicyEvaluationRunId = attendancePolicyRun?.Id,
			PayrollAttendanceSource = hasPublishedEvaluation ? "published_attendance_policy" : "raw_attendance",
			AttendancePolicyWarnings = itemAttendancePolicyWarnings,
			EarningComponents = computation.Earnings.Select(ToRunPayrollComponentItem).ToList(),
			DeductionComponents = computation.Deductions.Select(ToRunPayrollComponentItem).ToList()
		};
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

	private async Task<List<PayrollTargetEmployee>> ResolveScopedTargetEmployeesAsync(List<Guid>? requestedEmployeeIds)
	{
		var scopedEmployees = await AsyncExecuter.ToListAsync(
			(await GetScopedActiveEmployeeQueryAsync(includeUnknownStageEmployees: false))
				.Select(e => new PayrollTargetEmployee(e.Id, e.EmployeeCode, e.EmployeeName))
		);

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
			Amount = (double)item.Amount
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
		decimal? netPayDeltaPercent)
	{
		var issues = new List<string>();

		if (payrollDetails == null || components.Count == 0)
		{
			issues.Add(IssueCodes.MissingSalaryStructure);
		}

		if (basisDays > 0 && payableDays <= 0)
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

	private static string ToAttendancePolicyIssueCode(string warning)
	{
		if (warning.Contains("no published evaluation", StringComparison.OrdinalIgnoreCase))
		{
			return IssueCodes.AttendancePolicyEvaluationMissing;
		}

		if (warning.Contains("no evaluation results", StringComparison.OrdinalIgnoreCase))
		{
			return IssueCodes.AttendancePolicyEvaluationResultMissing;
		}

		if (warning.Contains("older attendance configuration", StringComparison.OrdinalIgnoreCase))
		{
			return IssueCodes.AttendancePolicyEvaluationConfigStale;
		}

		return IssueCodes.AttendancePolicyEffectiveBoundary;
	}

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

	private async Task<IQueryable<Employee>> GetScopedActiveEmployeeQueryAsync(bool includeUnknownStageEmployees)
	{
		var scope = await EnsureCanOperatePayrollAsync();
		var employeeQuery = await _employeeRepository.GetQueryableAsync();

		if (CurrentTenant.Id.HasValue)
		{
			employeeQuery = employeeQuery.Where(e => e.TenantId == CurrentTenant.Id);
		}

		employeeQuery = employeeQuery.Where(e => e.EmployeeStatusType == EmployeeStatusType.Active);
		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return employeeQuery;
		}

		var attendanceQuery = await _attendanceRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			attendanceQuery = attendanceQuery.Where(a => a.TenantId == CurrentTenant.Id);
		}

		// Materialize the employee-stage map to keep scope checks root-aware without complex EF translation.
		var employeeStageRows = await AsyncExecuter.ToListAsync(
			attendanceQuery
				.Where(a => a.EducationMediumStageId.HasValue)
				.Select(a => new { a.EmployeeId, a.EducationMediumStageId })
		);

		var employeeStageMap = employeeStageRows
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(
				x => x.Key,
				x => (IReadOnlyCollection<Guid>)x
					.Select(row => row.EducationMediumStageId!.Value)
					.Distinct()
					.ToList());
		var knownStageEmployeeIds = employeeStageMap.Keys.ToList();
		var scopedStageEmployeeIds = await _hrScopeAccessService.FilterEmployeeIdsByStagesAsync(
			knownStageEmployeeIds,
			employeeStageMap,
			scope,
			includeUnknownStageEmployees: false);

		if (includeUnknownStageEmployees)
		{
			// Unknown stage records are treated as in-scope for read/list flows to avoid false-deny for newly onboarded staff.
			return employeeQuery.Where(e => scopedStageEmployeeIds.Contains(e.Id) || !knownStageEmployeeIds.Contains(e.Id));
		}

		// Deny unknown-stage employees for write/finalize flows.
		return employeeQuery.Where(e => scopedStageEmployeeIds.Contains(e.Id));
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

