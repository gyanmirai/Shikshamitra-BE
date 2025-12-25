using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.EventAndHolidays;
using ShikshaMitra.HR.Payroll.SalarySlips;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

/// <summary>
/// Application service for running payroll - generates preview and locks payroll for a month
/// </summary>
public class RunPayrollAppService : ApplicationService, IRunPayrollAppService
{
	private readonly IRepository<Employee, Guid> _employeeRepository;
	private readonly IRepository<EmployeeAttendance, Guid> _attendanceRepository;
	private readonly IEmployeePayrollDetailsRepository _payrollDetailsRepository;
	private readonly IEmployeePayrollComponentRepository _payrollComponentRepository;
	private readonly ISalarySlipRepository _salarySlipRepository;
	private readonly IRepository<EventAndHoliday, Guid> _holidayRepository;
	private readonly SalarySlipManager _salarySlipManager;
	private readonly ILogger<RunPayrollAppService> _logger;

	public RunPayrollAppService(
		IRepository<Employee, Guid> employeeRepository,
		IRepository<EmployeeAttendance, Guid> attendanceRepository,
		IEmployeePayrollDetailsRepository payrollDetailsRepository,
		IEmployeePayrollComponentRepository payrollComponentRepository,
		ISalarySlipRepository salarySlipRepository,
		IRepository<EventAndHoliday, Guid> holidayRepository,
		SalarySlipManager salarySlipManager,
		ILogger<RunPayrollAppService> logger)
	{
		_employeeRepository = employeeRepository;
		_attendanceRepository = attendanceRepository;
		_payrollDetailsRepository = payrollDetailsRepository;
		_payrollComponentRepository = payrollComponentRepository;
		_salarySlipRepository = salarySlipRepository;
		_holidayRepository = holidayRepository;
		_salarySlipManager = salarySlipManager;
		_logger = logger;
	}

	/// <summary>
	/// Get payroll preview for a specific month (paginated)
	/// </summary>
	public async Task<RunPayrollResponse> GetPreviewAsync(GetRunPayrollInput input)
	{
		// Parse and validate month
		var (year, month, internalMonthYear) = ParseMonth(input.Month);

		_logger.LogInformation("Generating payroll preview for {Month} (Year: {Year}, Month: {MonthNum})",
			input.Month, year, month);

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

		// Calculate total working days in the month (business days - holidays)
		var totalWorkingDays = await CalculateTotalWorkingDaysAsync(year, month);

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
			componentsByEmployee[employee.Id].ToList(), // Lookup returns empty collection if key not found
			totalWorkingDays
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

	/// <summary>
	/// Generate and lock payroll for a month
	/// Uses batch processing for better performance with large employee counts
	/// </summary>
	public async Task<PayrollLockResponse> LockPayrollAsync(RunPayrollRequest request)
	{
		// Parse and validate month
		var (year, month, internalMonthYear) = ParseMonth(request.Month);

		_logger.LogInformation("Locking payroll for {Month} (Year: {Year}, Month: {MonthNum})",
			request.Month, year, month);

		// Check if payroll is already generated
		var isGenerated = await _salarySlipRepository.HasFinalizedSlipsForMonthAsync(internalMonthYear);
		if (isGenerated)
		{
			throw new UserFriendlyException($"Payroll for {request.Month} is already generated and locked.");
		}

		// Get all active employees
		var employeeQuery = await _employeeRepository.GetQueryableAsync();
		
		if (CurrentTenant.Id.HasValue)
		{
			employeeQuery = employeeQuery.Where(e => e.TenantId == CurrentTenant.Id);
		}

		var employees = await AsyncExecuter.ToListAsync(
			employeeQuery
				.AsNoTracking() // Read-only query
				.Where(e => e.EmployeeStatusType == EmployeeStatusType.Active)
				.Select(e => new { e.Id, e.EmployeeCode, e.EmployeeName }) // Project only needed fields
		);

		if (employees.Count == 0)
		{
			throw new UserFriendlyException("No active employees found to generate payroll.");
		}

		_logger.LogInformation("Generating salary slips for {Count} employees", employees.Count);

		var generatedCount = 0;
		var errors = new List<string>();
		const int batchSize = 50; // Process in batches for progress tracking

		// Process employees in batches
		for (int i = 0; i < employees.Count; i += batchSize)
		{
			var batch = employees.Skip(i).Take(batchSize).ToList();
			
			_logger.LogInformation("Processing batch {BatchNum}/{TotalBatches} ({Count} employees)", 
				(i / batchSize) + 1, 
				(int)Math.Ceiling((double)employees.Count / batchSize),
				batch.Count);

			// Process each employee in the batch sequentially (DbContext is not thread-safe)
			foreach (var employee in batch)
			{
				try
				{
					// Generate or get existing salary slip (autoSave is handled by SalarySlipManager)
					var salarySlip = await _salarySlipManager.GenerateOrGetSalarySlipAsync(
						employee.Id, 
						internalMonthYear, 
						forceRegenerate: true);

					// Finalize the salary slip
					if (salarySlip.Status != SalarySlipStatus.Finalized)
					{
						salarySlip.Finalize();
						await _salarySlipRepository.UpdateAsync(salarySlip, autoSave: false);
					}

					generatedCount++;
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error generating salary slip for employee {EmployeeId} ({EmployeeName})",
						employee.Id, employee.EmployeeName);
					errors.Add($"Failed for {employee.EmployeeCode}: {ex.Message}");
				}
			}

			// Save changes for this batch (reduces DB round-trips)
			try
			{
				await UnitOfWorkManager.Current.SaveChangesAsync();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error saving batch changes");
				throw new UserFriendlyException("Failed to save payroll changes. Please try again.");
			}
		}

		if (errors.Count > 0 && generatedCount == 0)
		{
			throw new UserFriendlyException($"Payroll generation failed for all employees. First error: {errors.First()}");
		}

		_logger.LogInformation("Payroll locked for {Month}. Generated: {Generated}, Failed: {Failed}",
			request.Month, generatedCount, errors.Count);

		var message = errors.Count > 0
			? $"Payroll generated and locked for {generatedCount} employees. {errors.Count} employees failed."
			: "Payroll generated and locked successfully";

		return new PayrollLockResponse
		{
			Message = message,
			Month = request.Month
		};
	}

	#region Private Methods

	/// <summary>
	/// Parse month from MMMM-YYYY format to year, month number, and internal format
	/// </summary>
	private (int year, int month, string internalFormat) ParseMonth(string monthYear)
	{
		if (string.IsNullOrWhiteSpace(monthYear))
		{
			throw new UserFriendlyException("Month parameter is required");
		}

		// Expected format: JANUARY-2025
		var parts = monthYear.Split('-');
		if (parts.Length != 2)
		{
			throw new UserFriendlyException("Invalid month format. Expected: MMMM-YYYY (e.g., JANUARY-2025)");
		}

		var monthName = parts[0];
		var yearStr = parts[1];

		if (!int.TryParse(yearStr, out var year) || year < 1900 || year > 2100)
		{
			throw new UserFriendlyException("Invalid year. Must be between 1900 and 2100.");
		}

		// Parse month name
		if (!DateTime.TryParseExact(monthName, "MMMM", CultureInfo.InvariantCulture, 
			DateTimeStyles.None, out var date))
		{
			throw new UserFriendlyException($"Invalid month name: {monthName}. Expected format: JANUARY, FEBRUARY, etc.");
		}

		var month = date.Month;
		var internalFormat = $"{year:D4}-{month:D2}"; // YYYY-MM format

		return (year, month, internalFormat);
	}

	/// <summary>
	/// Calculate total working days in a month (business days minus holidays)
	/// Uses HashSet to avoid double-counting overlapping holidays
	/// </summary>
	private async Task<int> CalculateTotalWorkingDaysAsync(int year, int month)
	{
		var firstDay = new DateTime(year, month, 1);
		var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));

		// Count business days (Monday to Friday)
		var businessDays = 0;
		for (var date = firstDay; date <= lastDay; date = date.AddDays(1))
		{
			if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
			{
				businessDays++;
			}
		}

		// Get holidays for this month
		var holidayQuery = await _holidayRepository.GetQueryableAsync();
		
		if (CurrentTenant.Id.HasValue)
		{
			holidayQuery = holidayQuery.Where(h => h.TenantId == CurrentTenant.Id);
		}

		var startDate = new DateTimeOffset(firstDay);
		var endDate = new DateTimeOffset(lastDay.AddDays(1).AddTicks(-1));

		var holidays = await AsyncExecuter.ToListAsync(
			holidayQuery.Where(h => 
				h.IsHoliday &&
				h.StartDate <= endDate &&
				h.EndDate >= startDate)
		);

		// Use HashSet to track unique holiday dates (avoids double-counting overlapping holidays)
		var holidayDates = new HashSet<DateTime>();
		foreach (var holiday in holidays)
		{
			var hStart = holiday.StartDate.Date > firstDay ? holiday.StartDate.Date : firstDay;
			var hEnd = holiday.EndDate.Date < lastDay ? holiday.EndDate.Date : lastDay;

			for (var date = hStart; date <= hEnd; date = date.AddDays(1))
			{
				// Only count weekdays, and HashSet ensures no duplicates
				if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
				{
					holidayDates.Add(date);
				}
			}
		}

		return Math.Max(0, businessDays - holidayDates.Count);
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
				.AsNoTracking() // Read-only query for performance
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
		int totalWorkingDays)
	{
		var basicSalary = payrollDetails?.BasicSalary ?? 0m;

		// Single-pass calculation for earnings and deductions (optimized)
		var grossSalary = 0m;
		var totalDeduction = 0m;
		foreach (var component in components)
		{
			if (component.IsEarning)
				grossSalary += component.Amount;
			else
				totalDeduction += component.Amount;
		}
		
		var netPay = grossSalary - totalDeduction;

		// Calculate attendance
		var presentDays = attendanceRecords.Count(a => a.StatusType == EmployeeAttendanceStatusType.Present);
		var halfDays = attendanceRecords.Count(a => a.StatusType == EmployeeAttendanceStatusType.HalfDayLeave);

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
			TotalWorkingDays = totalWorkingDays
		};
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

