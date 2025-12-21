using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Employees;
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
				throw new UserFriendlyException("EmployeeId is required");
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

			// Verify employee exists with detailed logging
			var employee = await _employeeRepository.GetAsync(employeeId, includeDetails: false);
			if (employee == null)
			{
				_logger.LogWarning("Employee not found - EmployeeId: {EmployeeId}", employeeId);
				throw new UserFriendlyException($"Employee with ID {employeeId} not found");
			}

			// Generate or retrieve salary slip
			var salarySlip = await _salarySlipManager.GenerateOrGetSalarySlipAsync(
				employeeId, 
				monthYear, 
				forceRegenerate: false);

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
		catch (UserFriendlyException ex)
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
			throw new UserFriendlyException("An error occurred while fetching the salary slip. Please try again.");
		}
	}

	/// <summary>
	/// Maps SalarySlip entity to DTO with employee details
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
			Earnings = salarySlip.Earnings.Select(e => new SalarySlipLineItemDto
			{
				Name = e.Name,
				Amount = e.Amount,
				Description = e.Description
			}).ToList(),
			Deductions = salarySlip.Deductions.Select(d => new SalarySlipLineItemDto
			{
				Name = d.Name,
				Amount = d.Amount,
				Description = d.Description
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
