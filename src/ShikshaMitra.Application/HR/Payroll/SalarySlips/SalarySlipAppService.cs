using Microsoft.Extensions.Logging;
using ShikshaMitra.HR;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.Security.UserScope;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.Payroll.Settings;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

/// <summary>
/// Application service for salary slip operations with caching and performance optimizations
/// </summary>
[RemoteService(false)]
public class SalarySlipAppService : ApplicationService, ISalarySlipAppService
{
	private readonly ISalarySlipRepository _salarySlipRepository;
	private readonly IEmployeeRepository _employeeRepository;
	private readonly IRepository<EmployeeAttendance, Guid> _employeeAttendanceRepository;
	private readonly IRepository<PayrollRunItemSnapshot, Guid> _payrollRunItemSnapshotRepository;
	private readonly IEmployeePayrollDetailsRepository _employeePayrollDetailsRepository;
	private readonly SalarySlipManager _salarySlipManager;
	private readonly PayslipPdfService _payslipPdfService;
	private readonly HrPolicyResolver _hrPolicyResolver;
	private readonly PayrollCalendarResolver _payrollCalendarResolver;
	private readonly IUserScopeService _userScopeService;
	private readonly HrUserScopeAccessService _hrScopeAccessService;
	private readonly ILogger<SalarySlipAppService> _logger;

	public SalarySlipAppService(
		ISalarySlipRepository salarySlipRepository,
		IEmployeeRepository employeeRepository,
		IRepository<EmployeeAttendance, Guid> employeeAttendanceRepository,
		IRepository<PayrollRunItemSnapshot, Guid> payrollRunItemSnapshotRepository,
		IEmployeePayrollDetailsRepository employeePayrollDetailsRepository,
		SalarySlipManager salarySlipManager,
		PayslipPdfService payslipPdfService,
		HrPolicyResolver hrPolicyResolver,
		PayrollCalendarResolver payrollCalendarResolver,
		IUserScopeService userScopeService,
		HrUserScopeAccessService hrScopeAccessService,
		ILogger<SalarySlipAppService> logger)
	{
		_salarySlipRepository = salarySlipRepository;
		_employeeRepository = employeeRepository;
		_employeeAttendanceRepository = employeeAttendanceRepository;
		_payrollRunItemSnapshotRepository = payrollRunItemSnapshotRepository;
		_employeePayrollDetailsRepository = employeePayrollDetailsRepository;
		_salarySlipManager = salarySlipManager;
		_payslipPdfService = payslipPdfService;
		_hrPolicyResolver = hrPolicyResolver;
		_payrollCalendarResolver = payrollCalendarResolver;
		_userScopeService = userScopeService;
		_hrScopeAccessService = hrScopeAccessService;
		_logger = logger;
	}

	/// <summary>
	/// Fetches salary slip with caching and performance monitoring
	/// </summary>
	public async Task<SalarySlipDto> GetSalarySlipAsync(Guid employeeId, string monthYear)
	{
		var stopwatch = Stopwatch.StartNew();

		try
		{
			// Input validation
			_logger.LogInformation("GetSalarySlip request - EmployeeId: {EmployeeId}, MonthYear: {MonthYear}",
				employeeId, monthYear);

			if (employeeId == Guid.Empty)
			{
				throw new BusinessException(HRErrorCodes.PayrollEmployeeIdRequired);
			}

			await EnsureEmployeeScopeAllowedAsync(employeeId);
			_salarySlipManager.ValidateMonthYearFormat(monthYear);
			var normalizedMonthYear = NormalizeMonthYear(monthYear);
			var hadExistingSalarySlip = await _salarySlipRepository.GetByEmployeeAndMonthAsync(
				employeeId,
				normalizedMonthYear) != null;

			// Generate or retrieve salary slip (Manager validates employee existence)
			var salarySlip = await _salarySlipManager.GenerateOrGetSalarySlipAsync(
				employeeId,
				monthYear,
				forceRegenerate: false);
			salarySlip = await EnsureDraftSalarySlipFreshAsync(salarySlip, hadExistingSalarySlip);

			// Get employee for DTO mapping (already validated by Manager)
			var employee = await _employeeRepository.GetAsync(salarySlip.EmployeeId, includeDetails: false);

			// Map to DTO
			var dto = await MapToSalarySlipDtoAsync(salarySlip, employee);

			stopwatch.Stop();
			_logger.LogInformation("Salary slip processed successfully - EmployeeId: {EmployeeId}, " +
				"MonthYear: {MonthYear}, Status: {Status}, TotalEarning: {TotalEarning}, " +
				"TotalDeduction: {TotalDeduction}, NetSalary: {NetSalary}, Duration: {Duration}ms",
				employeeId, monthYear, salarySlip.Status, salarySlip.TotalEarning,
				salarySlip.TotalDeduction, salarySlip.NetSalary, stopwatch.ElapsedMilliseconds);

			return dto;
		}
		catch (BusinessException ex)
		{
			stopwatch.Stop();
			_logger.LogWarning(ex, "User-friendly error in GetSalarySlip - EmployeeId: {EmployeeId}, " +
				"MonthYear: {MonthYear}, Duration: {Duration}ms",
				employeeId, monthYear, stopwatch.ElapsedMilliseconds);
			throw;
		}
		catch (Exception ex)
		{
			stopwatch.Stop();
			_logger.LogError(ex, "Unexpected error in GetSalarySlip - EmployeeId: {EmployeeId}, " +
				"MonthYear: {MonthYear}, Duration: {Duration}ms",
				employeeId, monthYear, stopwatch.ElapsedMilliseconds);
			throw new BusinessException(HRErrorCodes.SalarySlipFetchFailed)
				.WithData("employeeId", employeeId)
				.WithData("monthYear", monthYear);
		}
	}

	public async Task<byte[]> GenerateSalarySlipPdfAsync(Guid employeeId, string monthYear)
	{
		if (employeeId == Guid.Empty)
		{
			throw new BusinessException(HRErrorCodes.PayrollEmployeeIdRequired);
		}

		await EnsureEmployeeScopeAllowedAsync(employeeId);
		var normalizedMonthYear = NormalizeMonthYear(monthYear);
		var salarySlip = await _salarySlipRepository.GetByEmployeeAndMonthAsync(employeeId, normalizedMonthYear);
		if (salarySlip == null)
		{
			throw new BusinessException(HRErrorCodes.SalarySlipNotFound);
		}

		if (salarySlip.Status is not (SalarySlipStatus.Finalized or SalarySlipStatus.Paid))
		{
			throw new BusinessException(HRErrorCodes.SalarySlipPdfRequiresFinalized);
		}

		var employee = await _employeeRepository.GetAsync(salarySlip.EmployeeId, includeDetails: false);
		var dto = await MapToSalarySlipDtoAsync(salarySlip, employee);
		var period = FormatPayPeriod(dto.MonthYear);
		var pdfModel = new PayslipPdfModel(
			InstituteName: CurrentTenant.Name ?? "ShikshaMitra",
			InstituteAddress: null,
			EmployeeName: dto.EmployeeName ?? "-",
			EmployeeCode: dto.EmployeeCode ?? "-",
			Department: dto.Department,
			Designation: dto.Designation,
			DateOfJoining: dto.DateOfJoining?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
			PayPeriodDisplay: period,
			SlipNumber: dto.SlipNumber ?? BuildSlipNumber(dto.EmployeeCode, dto.MonthYear),
			GeneratedAt: dto.ProcessedDate,
			PayrollBasisDays: dto.PayrollBasisDays,
			PayableDays: dto.PayableDays,
			PresentDays: dto.PresentDays,
			HalfWorkingDays: dto.HalfWorkingDays,
			LopDays: dto.LopDays,
			ProrationFactor: dto.ProrationFactor,
			DayCalculationMode: FormatDayCalculationMode(dto.PayrollDayCalculationMode),
			BankName: dto.BankName,
			MaskedAccountNumber: dto.MaskedAccountNumber,
			IfscCode: dto.IfscCode,
			UanNumber: dto.UanNumber,
			EpfNumber: dto.EpfNumber,
			Earnings: dto.Earnings.Select(x => new PayslipPdfLineItem(x.Name, x.Amount)).ToList(),
			Deductions: dto.Deductions.Select(x => new PayslipPdfLineItem(x.Name, x.Amount)).ToList(),
			TotalEarnings: dto.TotalEarning,
			TotalDeductions: dto.TotalDeduction,
			NetPay: dto.NetSalary,
			NetPayInWords: dto.NetPayInWords ?? ToIndianCurrencyWords(dto.NetSalary),
			Remarks: dto.Remarks);

		return _payslipPdfService.Generate(pdfModel);
	}

	/// <summary>
	/// Generates a new salary slip or regenerates an existing draft
	/// </summary>
	public async Task<SalarySlipDto> GenerateSalarySlipAsync(GenerateSalarySlipInput input)
	{
		_logger.LogInformation("GenerateSalarySlip request - EmployeeId: {EmployeeId}, MonthYear: {MonthYear}, ForceRegenerate: {ForceRegenerate}",
			input.EmployeeId, input.MonthYear, input.ForceRegenerate);

		if (input.EmployeeId == Guid.Empty)
		{
			throw new BusinessException(HRErrorCodes.PayrollEmployeeIdRequired);
		}

		await EnsureEmployeeScopeAllowedAsync(input.EmployeeId);
		_salarySlipManager.ValidateMonthYearFormat(input.MonthYear);

		// Generate or regenerate salary slip (Manager validates employee existence)
		var salarySlip = await _salarySlipManager.GenerateOrGetSalarySlipAsync(
			input.EmployeeId,
			input.MonthYear,
			input.ForceRegenerate);

		// Get employee for DTO mapping (already validated by Manager)
		var employee = await _employeeRepository.GetAsync(salarySlip.EmployeeId, includeDetails: false);

		// Map to DTO
		var dto = await MapToSalarySlipDtoAsync(salarySlip, employee);

		_logger.LogInformation("Salary slip generated - Id: {Id}, EmployeeId: {EmployeeId}, MonthYear: {MonthYear}",
			salarySlip.Id, input.EmployeeId, input.MonthYear);

		return dto;
	}

	/// <summary>
	/// Gets a salary slip by its identifier
	/// </summary>
	public async Task<SalarySlipDto> GetAsync(Guid id)
	{
		_logger.LogInformation("GetSalarySlipById request - Id: {Id}", id);

		var salarySlip = await _salarySlipRepository.GetAsync(id);
		await EnsureEmployeeScopeAllowedAsync(salarySlip.EmployeeId);
		var employee = await _employeeRepository.GetAsync(salarySlip.EmployeeId, includeDetails: false);

		var dto = await MapToSalarySlipDtoAsync(salarySlip, employee);

		_logger.LogInformation("Salary slip retrieved - Id: {Id}, EmployeeId: {EmployeeId}, MonthYear: {MonthYear}",
			id, salarySlip.EmployeeId, salarySlip.MonthYear);

		return dto;
	}

	/// <summary>
	/// Updates remarks for a draft salary slip
	/// </summary>
	public async Task<SalarySlipDto> UpdateAsync(Guid id, UpdateSalarySlipInput input)
	{
		_logger.LogInformation("UpdateSalarySlip request - Id: {Id}", id);

		var salarySlip = await _salarySlipRepository.GetAsync(id);
		await EnsureEmployeeScopeAllowedAsync(salarySlip.EmployeeId);

		// Update remarks
		salarySlip.UpdateRemarks(input.Remarks);

		await _salarySlipRepository.UpdateAsync(salarySlip);

		// Get employee details for DTO
		var employee = await _employeeRepository.GetAsync(salarySlip.EmployeeId, includeDetails: false);
		var dto = await MapToSalarySlipDtoAsync(salarySlip, employee);

		_logger.LogInformation("Salary slip updated - Id: {Id}, EmployeeId: {EmployeeId}, MonthYear: {MonthYear}",
			id, salarySlip.EmployeeId, salarySlip.MonthYear);

		return dto;
	}

	/// <summary>
	/// Finalizes a salary slip, making it immutable
	/// </summary>
	public async Task<SalarySlipDto> FinalizeAsync(Guid id)
	{
		_logger.LogInformation("FinalizeSalarySlip request - Id: {Id}", id);

		var salarySlip = await _salarySlipRepository.GetAsync(id);
		await EnsureEmployeeScopeAllowedAsync(salarySlip.EmployeeId);

		// Finalize the salary slip
		salarySlip.Finalize();

		await _salarySlipRepository.UpdateAsync(salarySlip);

		// Get employee details for DTO
		var employee = await _employeeRepository.GetAsync(salarySlip.EmployeeId, includeDetails: false);
		var dto = await MapToSalarySlipDtoAsync(salarySlip, employee);

		_logger.LogInformation("Salary slip finalized - Id: {Id}, EmployeeId: {EmployeeId}, MonthYear: {MonthYear}",
			id, salarySlip.EmployeeId, salarySlip.MonthYear);

		return dto;
	}

	/// <summary>
	/// Deletes a draft salary slip
	/// </summary>
	public async Task DeleteAsync(Guid id)
	{
		_logger.LogInformation("DeleteSalarySlip request - Id: {Id}", id);

		var salarySlip = await _salarySlipRepository.GetAsync(id);
		await EnsureEmployeeScopeAllowedAsync(salarySlip.EmployeeId);

		if (salarySlip.Status == SalarySlipStatus.Finalized)
		{
			throw new BusinessException(HRErrorCodes.SalarySlipAlreadyFinalized);
		}

		await _salarySlipRepository.DeleteAsync(id);

		_logger.LogInformation("Salary slip deleted - Id: {Id}, EmployeeId: {EmployeeId}, MonthYear: {MonthYear}",
			id, salarySlip.EmployeeId, salarySlip.MonthYear);
	}

	private async Task<SalarySlip> EnsureDraftSalarySlipFreshAsync(
		SalarySlip salarySlip,
		bool hadExistingSalarySlip)
	{
		if (salarySlip.Status is SalarySlipStatus.Finalized or SalarySlipStatus.Paid)
		{
			return salarySlip;
		}

		if (hadExistingSalarySlip && IsHistoricalPayrollMonth(salarySlip.MonthYear))
		{
			return salarySlip;
		}

		var snapshot = await GetPayrollSnapshotAsync(salarySlip.EmployeeId, salarySlip.MonthYear);
		if (snapshot != null)
		{
			if (!snapshot.IsLocked)
			{
				if (!hadExistingSalarySlip)
				{
					return salarySlip;
				}

				var resolvedPolicies = await ResolvePoliciesForMonthAsync(salarySlip.EmployeeId, salarySlip.MonthYear);
				var unlockedSnapshotEarnings = PayrollSnapshotComponentParser.BuildSalarySlipLineItems(snapshot.EarningComponentsJson);
				var unlockedSnapshotDeductions = PayrollSnapshotComponentParser.BuildSalarySlipLineItems(snapshot.DeductionComponentsJson);
				if (PayrollSnapshotComponentParser.HasLegacyComponentSnapshotFormat(snapshot) ||
				    !SnapshotMatchesResolvedPolicies(snapshot, resolvedPolicies) ||
				    !SalarySlipMatchesSnapshot(salarySlip, snapshot, unlockedSnapshotEarnings, unlockedSnapshotDeductions))
				{
					return await _salarySlipManager.GenerateOrGetSalarySlipAsync(
						salarySlip.EmployeeId,
						salarySlip.MonthYear,
						forceRegenerate: true);
				}

				return salarySlip;
			}

			var snapshotEarnings = PayrollSnapshotComponentParser.BuildSalarySlipLineItems(snapshot.EarningComponentsJson);
			var snapshotDeductions = PayrollSnapshotComponentParser.BuildSalarySlipLineItems(snapshot.DeductionComponentsJson);
			if (!SalarySlipMatchesSnapshot(salarySlip, snapshot, snapshotEarnings, snapshotDeductions))
			{
				return await _salarySlipManager.GenerateOrGetSalarySlipFromSnapshotAsync(
					snapshot,
					snapshotEarnings,
					snapshotDeductions,
					ParseDayCalculationMode(snapshot.DayCalculationMode));
			}

			return salarySlip;
		}

		return await _salarySlipManager.GenerateOrGetSalarySlipAsync(
			salarySlip.EmployeeId,
			salarySlip.MonthYear,
			forceRegenerate: true);
	}

	private bool IsHistoricalPayrollMonth(string monthYear) =>
		string.Compare(
			NormalizeMonthYear(monthYear),
			Clock.Now.ToString("yyyy-MM", CultureInfo.InvariantCulture),
			StringComparison.Ordinal) < 0;

	private async Task<HrResolvedPolicies> ResolvePoliciesForMonthAsync(Guid employeeId, string monthYear)
	{
		var year = int.Parse(monthYear[..4], CultureInfo.InvariantCulture);
		var month = int.Parse(monthYear[5..7], CultureInfo.InvariantCulture);
		var calendar = await _payrollCalendarResolver.ResolveAsync(year, month);
		return await _hrPolicyResolver.ResolveAsync(
			employeeId,
			new DateTime(year, month, 1),
			calendar.AcademicYearId);
	}

	private static bool SnapshotMatchesResolvedPolicies(
		PayrollRunItemSnapshot snapshot,
		HrResolvedPolicies resolvedPolicies) =>
		snapshot.PayrollPolicyId == resolvedPolicies.PayrollPolicy.Id &&
		snapshot.PayrollPolicyVersion == resolvedPolicies.PayrollPolicy.Version &&
		snapshot.AttendancePolicyId == resolvedPolicies.AttendancePolicy.Id &&
		snapshot.AttendancePolicyVersion == resolvedPolicies.AttendancePolicy.Version &&
		snapshot.WorkCalendarPolicyId == resolvedPolicies.PayrollWorkCalendarPolicy.Id &&
		snapshot.WorkCalendarPolicyVersion == resolvedPolicies.PayrollWorkCalendarPolicy.Version;

	private static bool SalarySlipMatchesSnapshot(
		SalarySlip salarySlip,
		PayrollRunItemSnapshot snapshot,
		IReadOnlyList<SalarySlipLineItem> snapshotEarnings,
		IReadOnlyList<SalarySlipLineItem> snapshotDeductions) =>
		salarySlip.PayrollPolicyId == snapshot.PayrollPolicyId &&
		salarySlip.PayrollPolicyVersion == snapshot.PayrollPolicyVersion &&
		salarySlip.AttendancePolicyId == snapshot.AttendancePolicyId &&
		salarySlip.AttendancePolicyVersion == snapshot.AttendancePolicyVersion &&
		salarySlip.WorkCalendarPolicyId == snapshot.WorkCalendarPolicyId &&
		salarySlip.WorkCalendarPolicyVersion == snapshot.WorkCalendarPolicyVersion &&
		salarySlip.PayrollBasisDays == snapshot.BasisDays &&
		salarySlip.PayableDays == snapshot.PayableDays &&
		salarySlip.ProrationFactor == snapshot.ProrationFactor &&
		salarySlip.PayrollDayCalculationMode == ParseDayCalculationMode(snapshot.DayCalculationMode) &&
		Math.Round(salarySlip.TotalEarning, 2, MidpointRounding.AwayFromZero) == Math.Round(snapshot.GrossSalary, 2, MidpointRounding.AwayFromZero) &&
		Math.Round(salarySlip.TotalDeduction, 2, MidpointRounding.AwayFromZero) == Math.Round(snapshot.TotalDeduction, 2, MidpointRounding.AwayFromZero) &&
		Math.Round(salarySlip.NetSalary, 2, MidpointRounding.AwayFromZero) == Math.Round(snapshot.NetPay, 2, MidpointRounding.AwayFromZero) &&
		LineItemsMatch(salarySlip.Earnings, snapshotEarnings) &&
		LineItemsMatch(salarySlip.Deductions, snapshotDeductions);

	private static bool LineItemsMatch(
		IReadOnlyList<SalarySlipLineItem> current,
		IReadOnlyList<SalarySlipLineItem> snapshot)
	{
		if (current.Count != snapshot.Count)
		{
			return false;
		}

		for (var i = 0; i < current.Count; i++)
		{
			if (ToLineComparisonKey(current[i]) != ToLineComparisonKey(snapshot[i]))
			{
				return false;
			}
		}

		return true;
	}

	private static SalarySlipLineComparisonKey ToLineComparisonKey(SalarySlipLineItem item) =>
		new(
			item.Name ?? string.Empty,
			Math.Round(item.Amount, 2, MidpointRounding.AwayFromZero),
			item.Description ?? string.Empty,
			item.IsFixed,
			item.IsTemp,
			item.ComponentType,
			item.DependOn);

	private readonly record struct SalarySlipLineComparisonKey(
		string Name,
		decimal Amount,
		string Description,
		bool IsFixed,
		bool IsTemp,
		SalaryComponentType ComponentType,
		SalaryComponentDependency DependOn);

	private static DayCalculationType ParseDayCalculationMode(string value) => value switch
	{
		"working-days" => DayCalculationType.WorkingDays,
		"total-month-days" => DayCalculationType.TotalMonthDays,
		"manual-days" => DayCalculationType.ManualDays,
		_ => throw new BusinessException(HRErrorCodes.InvalidMonthYearFormat)
			.WithData("reason", "Invalid payroll day calculation mode on salary slip snapshot.")
			.WithData("dayCalculationMode", value)
	};

	/// <summary>
	/// Maps SalarySlip entity to DTO with employee details and component metadata
	/// </summary>
	private async Task<SalarySlipDto> MapToSalarySlipDtoAsync(SalarySlip salarySlip, Employee employee)
	{
		var snapshot = await GetPayrollSnapshotAsync(salarySlip.EmployeeId, salarySlip.MonthYear);
		var payrollDetails = await _employeePayrollDetailsRepository.GetByEmployeeIdAsync(salarySlip.EmployeeId);
		var payrollBasisDays = salarySlip.PayrollBasisDays ?? snapshot?.TotalWorkingDays;
		var payableDays = salarySlip.PayableDays ?? snapshot?.PayableDays;
		var presentDays = snapshot?.PresentDays;
		var halfWorkingDays = snapshot?.HalfWorkingDays;
		var lopDays = payrollBasisDays.HasValue && payableDays.HasValue
			? Math.Max(0m, payrollBasisDays.Value - payableDays.Value)
			: (decimal?) null;

		var dto = new SalarySlipDto
		{
			Id = salarySlip.Id,
			EmployeeId = salarySlip.EmployeeId,
			EmployeeName = employee.EmployeeName,
			EmployeeCode = employee.EmployeeCode,
			Department = employee.Department,
			Designation = employee.Designation,
			DateOfJoining = employee.DateOfJoining,
			SlipNumber = BuildSlipNumber(employee.EmployeeCode, salarySlip.MonthYear),
			MonthYear = salarySlip.MonthYear,
			TotalEarning = salarySlip.TotalEarning,
			TotalDeduction = salarySlip.TotalDeduction,
			NetSalary = salarySlip.NetSalary,
			Status = salarySlip.Status,
			Remarks = salarySlip.Remarks,
			ProcessedDate = salarySlip.ProcessedDate,
			PayrollBasisDays = payrollBasisDays,
			PayableDays = payableDays,
			PresentDays = presentDays,
			HalfWorkingDays = halfWorkingDays,
			LopDays = lopDays,
			ProrationFactor = salarySlip.ProrationFactor ?? snapshot?.ProrationFactor,
			PayrollDayCalculationMode = salarySlip.PayrollDayCalculationMode.HasValue
				? PayrollDayCalculationApiLabels.ToApiString(salarySlip.PayrollDayCalculationMode.Value)
				: snapshot?.DayCalculationMode,
			BankName = payrollDetails?.BankName,
			MaskedAccountNumber = MaskAccountNumber(payrollDetails?.AccountNumber),
			IfscCode = payrollDetails?.IfscCode,
			UanNumber = payrollDetails?.UanNumber,
			EpfNumber = payrollDetails?.EpfNumber,
			NetPayInWords = ToIndianCurrencyWords(salarySlip.NetSalary),
			Earnings = salarySlip.Earnings.Select(e => new SalarySlipLineItemDto
			{
				Name = e.Name,
				Amount = e.Amount,
				Description = e.Description,
				IsFixed = e.IsFixed,
				IsTemp = e.IsTemp,
				ComponentType = e.ComponentType.ToString(),
				DependOn = e.DependOn.ToString()
			}).ToList(),
			Deductions = salarySlip.Deductions.Select(d => new SalarySlipLineItemDto
			{
				Name = d.Name,
				Amount = d.Amount,
				Description = d.Description,
				IsFixed = d.IsFixed,
				IsTemp = d.IsTemp,
				ComponentType = d.ComponentType.ToString(),
				DependOn = d.DependOn.ToString()
			}).ToList()
		};

		return dto;
	}

	private async Task<PayrollRunItemSnapshot?> GetPayrollSnapshotAsync(Guid employeeId, string monthYear)
	{
		var query = await _payrollRunItemSnapshotRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			query = query.Where(x => x.TenantId == CurrentTenant.Id);
		}

		return await AsyncExecuter.FirstOrDefaultAsync(
			query.Where(x => x.EmployeeId == employeeId && x.MonthYear == monthYear));
	}

	private static string NormalizeMonthYear(string monthYear)
	{
		if (!PayrollMonthNormalizer.TryNormalizeToInternal(monthYear, out var normalizedMonthYear))
		{
			throw new BusinessException(string.IsNullOrWhiteSpace(monthYear)
				? HRErrorCodes.MonthYearRequired
				: HRErrorCodes.InvalidMonthYearFormat);
		}

		return normalizedMonthYear;
	}

	private static string BuildSlipNumber(string? employeeCode, string monthYear)
	{
		var normalizedCode = string.IsNullOrWhiteSpace(employeeCode) ? "EMP" : employeeCode.Trim();
		return $"PS-{monthYear.Replace("-", string.Empty, StringComparison.Ordinal)}-{normalizedCode}";
	}

	private static string? MaskAccountNumber(string? accountNumber)
	{
		if (string.IsNullOrWhiteSpace(accountNumber))
		{
			return null;
		}

		var digits = new string(accountNumber.Where(char.IsLetterOrDigit).ToArray());
		if (digits.Length <= 4)
		{
			return $"XXXX{digits}";
		}

		return $"XXXX{digits[^4..]}";
	}

	private static string FormatPayPeriod(string monthYear)
	{
		if (DateTime.TryParseExact(monthYear + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
			    DateTimeStyles.None, out var date))
		{
			return date.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
		}

		return monthYear;
	}

	private static string FormatDayCalculationMode(string? mode) =>
		mode switch
		{
			"working-days" => "Working days",
			"total-month-days" => "Calendar days",
			"manual-days" => "Manual days",
			null or "" => "-",
			_ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(mode.Replace("-", " ", StringComparison.Ordinal))
		};

	private static string ToIndianCurrencyWords(decimal amount)
	{
		var rupees = (long) Math.Floor(Math.Abs(amount));
		var paise = (int) Math.Round((Math.Abs(amount) - rupees) * 100, MidpointRounding.AwayFromZero);
		if (paise == 100)
		{
			rupees += 1;
			paise = 0;
		}

		var words = $"{NumberToWords(rupees)} rupees";
		if (paise > 0)
		{
			words += $" and {NumberToWords(paise)} paise";
		}

		return $"{CultureInfo.InvariantCulture.TextInfo.ToTitleCase(words)} only";
	}

	private static string NumberToWords(long number)
	{
		if (number == 0)
		{
			return "zero";
		}

		string[] units =
		[
			"", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
			"eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen",
			"eighteen", "nineteen"
		];
		string[] tens = ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

		static string TwoDigits(long value, string[] localUnits, string[] localTens)
		{
			if (value < 20)
			{
				return localUnits[(int) value];
			}

			return localTens[(int) (value / 10)] + (value % 10 > 0 ? " " + localUnits[(int) (value % 10)] : "");
		}

		static string ThreeDigits(long value, string[] localUnits, string[] localTens)
		{
			var hundred = value / 100;
			var rest = value % 100;
			var result = hundred > 0 ? localUnits[(int) hundred] + " hundred" : "";
			if (rest > 0)
			{
				result += (result.Length > 0 ? " " : "") + TwoDigits(rest, localUnits, localTens);
			}

			return result;
		}

		var parts = new List<string>();
		var crore = number / 10000000;
		number %= 10000000;
		var lakh = number / 100000;
		number %= 100000;
		var thousand = number / 1000;
		number %= 1000;

		if (crore > 0)
		{
			parts.Add($"{NumberToWords(crore)} crore");
		}

		if (lakh > 0)
		{
			parts.Add($"{NumberToWords(lakh)} lakh");
		}

		if (thousand > 0)
		{
			parts.Add($"{NumberToWords(thousand)} thousand");
		}

		if (number > 0)
		{
			parts.Add(ThreeDigits(number, units, tens));
		}

		return string.Join(" ", parts);
	}

	private async Task EnsureEmployeeScopeAllowedAsync(Guid employeeId)
	{
		var scope = await EnsureCanOperatePayrollAsync();
		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return;
		}

		var employeeStageId = await ResolveEmployeeStageAsync(employeeId);
		if (!employeeStageId.HasValue)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
		}

		if (!await _hrScopeAccessService.CanAccessEmployeeStageAsync(scope, employeeStageId.Value))
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
		}
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

	private async Task<Guid?> ResolveEmployeeStageAsync(Guid employeeId)
	{
		var attendanceQuery = await _employeeAttendanceRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			attendanceQuery = attendanceQuery.Where(a => a.TenantId == CurrentTenant.Id);
		}

		return await AsyncExecuter.FirstOrDefaultAsync(
			attendanceQuery
				.Where(a => a.EmployeeId == employeeId && a.EducationMediumStageId.HasValue)
				.OrderByDescending(a => a.AttendanceDate)
				.Select(a => a.EducationMediumStageId));
	}
}
