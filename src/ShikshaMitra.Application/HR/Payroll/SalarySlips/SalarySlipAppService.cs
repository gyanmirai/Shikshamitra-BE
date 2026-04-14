using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.Services;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

/// <summary>
/// Application service for salary slip operations with caching and performance optimizations
/// </summary>
[RemoteService(false)]
public class SalarySlipAppService : ApplicationService, ISalarySlipAppService
{
	private readonly ISalarySlipRepository _salarySlipRepository;
	private readonly IEmployeeRepository _employeeRepository;
	private readonly SalarySlipManager _salarySlipManager;
	private readonly IDistributedCache<SalarySlipDto> _cache;
	private readonly ILogger<SalarySlipAppService> _logger;

	public SalarySlipAppService(
		ISalarySlipRepository salarySlipRepository,
		IEmployeeRepository employeeRepository,
		SalarySlipManager salarySlipManager,
		IDistributedCache<SalarySlipDto> cache,
		ILogger<SalarySlipAppService> logger)
	{
		_salarySlipRepository = salarySlipRepository;
		_employeeRepository = employeeRepository;
		_salarySlipManager = salarySlipManager;
		_cache = cache;
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

			_salarySlipManager.ValidateMonthYearFormat(monthYear);

			// Check cache first
			var cacheKey = GetCacheKey(CurrentTenant.Id, employeeId, monthYear);
			var cachedDto = await _cache.GetAsync(cacheKey);

			if (cachedDto != null)
			{
				stopwatch.Stop();
				_logger.LogInformation("Salary slip retrieved from cache - EmployeeId: {EmployeeId}, " +
					"MonthYear: {MonthYear}, Duration: {Duration}ms",
					employeeId, monthYear, stopwatch.ElapsedMilliseconds);
				return cachedDto;
			}

			// Generate or retrieve salary slip (Manager validates employee existence)
			var salarySlip = await _salarySlipManager.GenerateOrGetSalarySlipAsync(
				employeeId,
				monthYear,
				forceRegenerate: false);

			// Get employee for DTO mapping (already validated by Manager)
			var employee = await _employeeRepository.GetAsync(salarySlip.EmployeeId, includeDetails: false);

			// Map to DTO
			var dto = await MapToSalarySlipDtoAsync(salarySlip, employee);

			// Cache the result (cache for finalized slips longer)
			var cacheOptions = new DistributedCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = salarySlip.Status == SalarySlipStatus.Finalized
					? TimeSpan.FromHours(24)  // Finalized slips: 24 hours
					: TimeSpan.FromMinutes(30) // Draft slips: 30 minutes
			};

			await _cache.SetAsync(cacheKey, dto, cacheOptions);

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

		_salarySlipManager.ValidateMonthYearFormat(input.MonthYear);

		// Generate or regenerate salary slip (Manager validates employee existence)
		var salarySlip = await _salarySlipManager.GenerateOrGetSalarySlipAsync(
			input.EmployeeId,
			input.MonthYear,
			input.ForceRegenerate);

		// Get employee for DTO mapping (already validated by Manager)
		var employee = await _employeeRepository.GetAsync(salarySlip.EmployeeId, includeDetails: false);

		// Clear cache
		var cacheKey = GetCacheKey(CurrentTenant.Id, input.EmployeeId, input.MonthYear);
		await _cache.RemoveAsync(cacheKey);

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

		// Update remarks
		salarySlip.UpdateRemarks(input.Remarks);

		await _salarySlipRepository.UpdateAsync(salarySlip);

		// Clear cache
		var cacheKey = GetCacheKey(CurrentTenant.Id, salarySlip.EmployeeId, salarySlip.MonthYear);
		await _cache.RemoveAsync(cacheKey);

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

		// Finalize the salary slip
		salarySlip.Finalize();

		await _salarySlipRepository.UpdateAsync(salarySlip);

		// Clear cache
		var cacheKey = GetCacheKey(CurrentTenant.Id, salarySlip.EmployeeId, salarySlip.MonthYear);
		await _cache.RemoveAsync(cacheKey);

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

		if (salarySlip.Status == SalarySlipStatus.Finalized)
		{
			throw new BusinessException(HRErrorCodes.SalarySlipAlreadyFinalized);
		}

		// Clear cache
		var cacheKey = GetCacheKey(CurrentTenant.Id, salarySlip.EmployeeId, salarySlip.MonthYear);
		await _cache.RemoveAsync(cacheKey);

		await _salarySlipRepository.DeleteAsync(id);

		_logger.LogInformation("Salary slip deleted - Id: {Id}, EmployeeId: {EmployeeId}, MonthYear: {MonthYear}",
			id, salarySlip.EmployeeId, salarySlip.MonthYear);
	}

	/// <summary>
	/// Maps SalarySlip entity to DTO with employee details and component metadata
	/// </summary>
	private Task<SalarySlipDto> MapToSalarySlipDtoAsync(SalarySlip salarySlip, Employee employee)
	{
		var dto = new SalarySlipDto
		{
			Id = salarySlip.Id,
			EmployeeId = salarySlip.EmployeeId,
			EmployeeName = employee.EmployeeName,
			EmployeeCode = employee.EmployeeCode,
			MonthYear = salarySlip.MonthYear,
			TotalEarning = salarySlip.TotalEarning,
			TotalDeduction = salarySlip.TotalDeduction,
			NetSalary = salarySlip.NetSalary,
			Status = salarySlip.Status.ToString(),
			Remarks = salarySlip.Remarks,
			ProcessedDate = salarySlip.ProcessedDate,
			PayrollBasisDays = salarySlip.PayrollBasisDays,
			PayableDays = salarySlip.PayableDays,
			ProrationFactor = salarySlip.ProrationFactor,
			PayrollDayCalculationMode = salarySlip.PayrollDayCalculationMode.HasValue
				? PayrollDayCalculationApiLabels.ToApiString(salarySlip.PayrollDayCalculationMode.Value)
				: null,
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

		return Task.FromResult(dto);
	}

	/// <summary>
	/// Generates cache key for salary slip (includes tenant ID for multi-tenancy support)
	/// </summary>
	private string GetCacheKey(Guid? tenantId, Guid employeeId, string monthYear)
	{
		var tenantPrefix = tenantId.HasValue ? tenantId.Value.ToString() : "host";
		return $"SalarySlip:{tenantPrefix}:{employeeId}:{monthYear}";
	}
}
