using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
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
	private readonly IRepository<Employee, Guid> _employeeRepository;
	private readonly IRepository<EmployeeAttendance, Guid> _attendanceRepository;
	private readonly IEmployeePayrollDetailsRepository _payrollDetailsRepository;
	private readonly IEmployeePayrollComponentRepository _payrollComponentRepository;
	private readonly ISalarySlipRepository _salarySlipRepository;
	private readonly IRepository<Holiday, Guid> _holidayRepository;
	private readonly IDayCalculationSettingRepository _dayCalculationSettingRepository;
	private readonly SalarySlipManager _salarySlipManager;
	private readonly ILogger<RunPayrollAppService> _logger;

	// Configuration: Batch size for processing employees
	private const int DefaultBatchSize = 50;
	private const int MaxBatchSize = 100;

	public RunPayrollAppService(
		IRepository<Employee, Guid> employeeRepository,
		IRepository<EmployeeAttendance, Guid> attendanceRepository,
		IEmployeePayrollDetailsRepository payrollDetailsRepository,
		IEmployeePayrollComponentRepository payrollComponentRepository,
		ISalarySlipRepository salarySlipRepository,
		IRepository<Holiday, Guid> holidayRepository,
		IDayCalculationSettingRepository dayCalculationSettingRepository,
		SalarySlipManager salarySlipManager,
		ILogger<RunPayrollAppService> logger)
	{
		_employeeRepository = employeeRepository;
		_attendanceRepository = attendanceRepository;
		_payrollDetailsRepository = payrollDetailsRepository;
		_payrollComponentRepository = payrollComponentRepository;
		_salarySlipRepository = salarySlipRepository;
		_holidayRepository = holidayRepository;
		_dayCalculationSettingRepository = dayCalculationSettingRepository;
		_salarySlipManager = salarySlipManager;
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
			throw new BusinessException("HR:InvalidPageSize")
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

			// Check if payroll is already generated (any finalized slip exists)
			var isGenerated = await _salarySlipRepository.HasFinalizedSlipsForMonthAsync(internalMonthYear);

			// Get all active employees
			var employeeQuery = await _employeeRepository.GetQueryableAsync();

			if (CurrentTenant.Id.HasValue)
			{
				employeeQuery = employeeQuery.Where(e => e.TenantId == CurrentTenant.Id);
			}

			// Filter only active employees
			employeeQuery = employeeQuery.Where(e => e.EmployeeStatusType == EmployeeStatusType.Active);

			// Get total count
			var totalCount = await AsyncExecuter.CountAsync(employeeQuery);

			// Get paginated employees
			var employees = await AsyncExecuter.ToListAsync(
				employeeQuery
					.OrderBy(e => e.FirstName)
					.ThenBy(e => e.LastName)
					.Skip(input.Page * input.PageSize)
					.Take(input.PageSize)
			);

			if (employees.Count == 0)
			{
				return new RunPayrollResponse
				{
					IsGenerated = isGenerated,
					TotalCount = totalCount,
					CurrentPage = input.Page,
					PageSize = input.PageSize,
					TotalPages = (int)Math.Ceiling((double)totalCount / input.PageSize),
					Items = new List<RunPayrollItem>()
				};
			}

			var holidays = await LoadHolidaysForMonthAsync(year, month);
			var workingWeekdaysMinusHolidays = PayrollMonthCalendar.CountWorkingWeekdaysExcludingHolidayWeekdays(year, month, holidays);
			var daySetting = await _dayCalculationSettingRepository.FindByTenantAsync(CurrentTenant.Id);
			var basis = PayrollBasisDays.Resolve(daySetting, year, month, workingWeekdaysMinusHolidays);

			// Get all employee IDs for batch operations
			var employeeIds = employees.Select(e => e.Id).ToList();
			var startDate = DateOnly.FromDateTime(new DateTime(year, month, 1));
			var endDate = DateOnly.FromDateTime(new DateTime(year, month, DateTime.DaysInMonth(year, month)));

			// BATCH LOAD: Get all data in parallel to avoid N+1 queries
			var attendanceTask = GetAttendanceRecordsAsync(employeeIds, startDate, endDate);
			var payrollDetailsTask = _payrollDetailsRepository.GetByEmployeeIdsAsync(employeeIds);
			var payrollComponentsTask = _payrollComponentRepository.GetByEmployeeIdsAsync(employeeIds);

			await Task.WhenAll(attendanceTask, payrollDetailsTask, payrollComponentsTask);

			var attendanceByEmployee = await attendanceTask;
			var payrollDetailsByEmployee = await payrollDetailsTask;
			var componentsByEmployee = await payrollComponentsTask;

			// Build payroll items (no additional DB calls - all data pre-loaded)
			var items = employees.Select(employee => BuildPayrollItem(
				employee,
				attendanceByEmployee.GetValueOrDefault(employee.Id) ?? new List<EmployeeAttendance>(),
				payrollDetailsByEmployee.GetValueOrDefault(employee.Id),
				componentsByEmployee[employee.Id]?.ToList() ?? new List<EmployeePayrollComponent>(),
				basis
			)).ToList();

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
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected error generating payroll preview for month {Month}", input.Month);
			throw new BusinessException("HR:PayrollPreviewFailed")
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

		// Check if payroll is already generated
		var isGenerated = await _salarySlipRepository.HasFinalizedSlipsForMonthAsync(internalMonthYear);
		if (isGenerated)
		{
			_logger.LogInformation("Payroll for {Month} is already locked. Returning idempotent response.", request.Month);

			return new PayrollLockResponse
			{
				Message = $"Payroll for {request.Month} is already generated and locked.",
				Month = request.Month,
				WasAlreadyLocked = true,
				EmployeesProcessed = 0
			};
		}

		// Get all active employees
		var employeeQuery = await _employeeRepository.GetQueryableAsync();

		if (CurrentTenant.Id.HasValue)
		{
			employeeQuery = employeeQuery.Where(e => e.TenantId == CurrentTenant.Id);
		}

		var employees = await AsyncExecuter.ToListAsync(
			employeeQuery
				.Where(e => e.EmployeeStatusType == EmployeeStatusType.Active)
				.Select(e => new { e.Id, e.EmployeeCode, e.EmployeeName })
		);

		if (employees.Count == 0)
		{
			_logger.LogWarning("No active employees found for payroll generation in {Month}", request.Month);
			throw new BusinessException("HR:NoActiveEmployees")
				.WithData("month", request.Month);
		}

		_logger.LogInformation("Generating salary slips for {Count} active employees", employees.Count);

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
					}
					else
					{
						salarySlip.Finalize();
						await _salarySlipRepository.UpdateAsync(salarySlip, autoSave: false);
						generatedCount++;
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
					throw new BusinessException("HR:PayrollBatchSaveFailed")
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
				throw new BusinessException("HR:PayrollGenerationCompleteFailure")
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

	private async Task<List<Holiday>> LoadHolidaysForMonthAsync(int year, int month)
	{
		var firstDay = new DateTime(year, month, 1);
		var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));
		var startDate = new DateTimeOffset(firstDay);
		var endDate = new DateTimeOffset(lastDay.AddDays(1).AddTicks(-1));

		var holidayQuery = await _holidayRepository.GetQueryableAsync();

		if (CurrentTenant.Id.HasValue)
		{
			holidayQuery = holidayQuery.Where(h => h.TenantId == CurrentTenant.Id);
		}

		return await AsyncExecuter.ToListAsync(
			holidayQuery.Where(h =>
				h.StartDate <= endDate &&
				h.EndDate >= startDate)
		);
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

	/// <summary>
	/// Build payroll item for an employee using pre-loaded data (no DB calls)
	/// </summary>
	private static RunPayrollItem BuildPayrollItem(
		Employee employee,
		List<EmployeeAttendance> attendanceRecords,
		EmployeePayrollDetails? payrollDetails,
		List<EmployeePayrollComponent> components,
		PayrollBasisDaysResult basis)
	{
		var presentDays = attendanceRecords.Count(a => a.StatusType == EmployeeAttendanceStatusType.Present);
		var halfDays = attendanceRecords.Count(a => a.StatusType == EmployeeAttendanceStatusType.HalfDayLeave);
		var payableDays = PayrollProrationCalculator.CalculatePayableDays(presentDays, halfDays);
		var factor = PayrollProrationCalculator.CalculateProrationFactor(basis.BasisDays, payableDays);

		var basicSalary = components
			.Where(c => c.IsEarning && IsBasicSalaryComponentName(c.Name))
			.Sum(c => c.Amount);
		basicSalary = PayrollProrationCalculator.ProrateAmount(basicSalary, isTemp: false, factor);

		var grossSalary = 0m;
		var totalDeduction = 0m;
		var earningComponents = new List<RunPayrollComponentItem>(components.Count);
		var deductionComponents = new List<RunPayrollComponentItem>(components.Count);
		foreach (var component in components)
		{
			var prorated = PayrollProrationCalculator.ProrateAmount(component.Amount, component.IsTemp, factor);
			var item = new RunPayrollComponentItem
			{
				Name = component.Name,
				Amount = (double)prorated
			};
			if (component.IsEarning)
			{
				grossSalary += prorated;
				earningComponents.Add(item);
			}
			else
			{
				totalDeduction += prorated;
				deductionComponents.Add(item);
			}
		}

		var netPay = grossSalary - totalDeduction;

		return new RunPayrollItem
		{
			EmployeeName = FormatEmployeeName(employee),
			EmployeeIdentifier = employee.EmployeeCode ?? string.Empty,
			IsTeaching = employee.EmployeeGroup == EmployeeGroupType.Teaching,
			BasicSalary = (double)basicSalary,
			GrossSalary = (double)grossSalary,
			TotalDeduction = (double)totalDeduction,
			NetPay = (double)netPay,
			PresentDays = presentDays,
			HalfWorkingDays = halfDays,
			TotalWorkingDays = basis.BasisDays,
			PayableDays = (double)payableDays,
			ProrationFactor = (double)factor,
			DayCalculationMode = PayrollDayCalculationApiLabels.ToApiString(basis.EffectiveMode),
			UsedDefaultDayCalculation = basis.UsedTenantDefault,
			EarningComponents = earningComponents,
			DeductionComponents = deductionComponents
		};
	}

	private static bool IsBasicSalaryComponentName(string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return false;
		}

		return string.Equals(name, "Basic", StringComparison.OrdinalIgnoreCase)
			|| name.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase);
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
}

