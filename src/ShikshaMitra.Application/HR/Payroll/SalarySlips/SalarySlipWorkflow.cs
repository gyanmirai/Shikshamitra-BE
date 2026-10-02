using Microsoft.Extensions.Logging;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Employees;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Uow;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Statutory;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

[RemoteService(false)]
public class SalarySlipWorkflow(
	ISalarySlipRepository salarySlipRepository,
	IEmployeeRepository employeeRepository,
	SalarySlipManager salarySlipManager,
	SalarySlipDraftFreshnessService draftFreshnessService,
	SalarySlipSnapshotGenerationService snapshotGenerationService,
	SalarySlipDtoMapper salarySlipDtoMapper,
	SalarySlipAccessService accessService,
	IPayrollInputRevisionStore revisions,
	EsiContinuityInvalidator continuityInvalidator,
	EsiEnrollmentResolver enrollmentResolver,
	ILogger<SalarySlipAppService> logger)
	: ApplicationService
{
	public async Task<SalarySlipDto> GetSalarySlipAsync(Guid employeeId, string monthYear)
	{
		var stopwatch = Stopwatch.StartNew();

		try
		{
			logger.LogInformation("GetSalarySlip request - EmployeeId: {EmployeeId}, MonthYear: {MonthYear}",
				employeeId, monthYear);

			if (employeeId == Guid.Empty)
			{
				throw new BusinessException(HRErrorCodes.PayrollEmployeeIdRequired);
			}

			await accessService.EnsureEmployeeScopeAllowedAsync(employeeId);
			salarySlipManager.ValidateMonthYearFormat(monthYear);
			var normalizedMonthYear = SalarySlipMonthYearNormalizer.Normalize(monthYear);
			var existingSalarySlip = await salarySlipRepository.GetByEmployeeAndMonthAsync(
				employeeId,
				normalizedMonthYear);

			if (existingSalarySlip?.Status is SalarySlipStatus.Finalized or SalarySlipStatus.Paid)
			{
				var existingEmployee = await employeeRepository.GetAsync(existingSalarySlip.EmployeeId, includeDetails: false);
				return await salarySlipDtoMapper.ToDtoAsync(existingSalarySlip, existingEmployee);
			}

			var salarySlip = existingSalarySlip == null
				? await snapshotGenerationService.GenerateOrGetFromSnapshotAsync(
					employeeId,
					normalizedMonthYear)
				: await draftFreshnessService.EnsureFreshAsync(existingSalarySlip);

			var employee = await employeeRepository.GetAsync(salarySlip.EmployeeId, includeDetails: false);
			var dto = await salarySlipDtoMapper.ToDtoAsync(salarySlip, employee);

			stopwatch.Stop();
			logger.LogInformation("Salary slip processed successfully - EmployeeId: {EmployeeId}, " +
				"MonthYear: {MonthYear}, Status: {Status}, TotalEarning: {TotalEarning}, " +
				"TotalDeduction: {TotalDeduction}, NetSalary: {NetSalary}, Duration: {Duration}ms",
				employeeId, monthYear, salarySlip.Status, salarySlip.TotalEarning,
				salarySlip.TotalDeduction, salarySlip.NetSalary, stopwatch.ElapsedMilliseconds);

			return dto;
		}
		catch (BusinessException ex)
		{
			stopwatch.Stop();
			logger.LogWarning(ex, "User-friendly error in GetSalarySlip - EmployeeId: {EmployeeId}, " +
				"MonthYear: {MonthYear}, Duration: {Duration}ms",
				employeeId, monthYear, stopwatch.ElapsedMilliseconds);
			throw;
		}
		catch (Exception ex)
		{
			stopwatch.Stop();
			logger.LogError(ex, "Unexpected error in GetSalarySlip - EmployeeId: {EmployeeId}, " +
				"MonthYear: {MonthYear}, Duration: {Duration}ms",
				employeeId, monthYear, stopwatch.ElapsedMilliseconds);
			throw new BusinessException(HRErrorCodes.SalarySlipFetchFailed)
				.WithData("employeeId", employeeId)
				.WithData("monthYear", monthYear);
		}
	}

	public async Task<SalarySlipDto> GenerateSalarySlipAsync(GenerateSalarySlipInput input)
	{
		logger.LogInformation("GenerateSalarySlip request - EmployeeId: {EmployeeId}, MonthYear: {MonthYear}, ForceRegenerate: {ForceRegenerate}",
			input.EmployeeId, input.MonthYear, input.ForceRegenerate);

		if (input.EmployeeId == Guid.Empty)
		{
			throw new BusinessException(HRErrorCodes.PayrollEmployeeIdRequired);
		}

		await accessService.EnsureEmployeeScopeAllowedAsync(input.EmployeeId);
		salarySlipManager.ValidateMonthYearFormat(input.MonthYear);

		var salarySlip = await snapshotGenerationService.GenerateOrGetFromSnapshotAsync(
			input.EmployeeId,
			input.MonthYear);

		var employee = await employeeRepository.GetAsync(salarySlip.EmployeeId, includeDetails: false);
		var dto = await salarySlipDtoMapper.ToDtoAsync(salarySlip, employee);

		logger.LogInformation("Salary slip generated - Id: {Id}, EmployeeId: {EmployeeId}, MonthYear: {MonthYear}",
			salarySlip.Id, input.EmployeeId, input.MonthYear);

		return dto;
	}

	public async Task<SalarySlipDto> GetAsync(Guid id)
	{
		logger.LogInformation("GetSalarySlipById request - Id: {Id}", id);

		var salarySlip = await salarySlipRepository.GetAsync(id);
		await accessService.EnsureEmployeeScopeAllowedAsync(salarySlip.EmployeeId);
		salarySlip = await draftFreshnessService.EnsureFreshAsync(salarySlip);
		var employee = await employeeRepository.GetAsync(salarySlip.EmployeeId, includeDetails: false);

		var dto = await salarySlipDtoMapper.ToDtoAsync(salarySlip, employee);

		logger.LogInformation("Salary slip retrieved - Id: {Id}, EmployeeId: {EmployeeId}, MonthYear: {MonthYear}",
			id, salarySlip.EmployeeId, salarySlip.MonthYear);

		return dto;
	}

	public async Task<SalarySlipDto> UpdateAsync(Guid id, UpdateSalarySlipInput input)
	{
		logger.LogInformation("UpdateSalarySlip request - Id: {Id}", id);

		var salarySlip = await salarySlipRepository.GetAsync(id);
		await accessService.EnsureEmployeeScopeAllowedAsync(salarySlip.EmployeeId);
		salarySlip = await draftFreshnessService.EnsureFreshAsync(salarySlip);

		salarySlip.UpdateRemarks(input.Remarks);

		await salarySlipRepository.UpdateAsync(salarySlip);

		var employee = await employeeRepository.GetAsync(salarySlip.EmployeeId, includeDetails: false);
		var dto = await salarySlipDtoMapper.ToDtoAsync(salarySlip, employee);

		logger.LogInformation("Salary slip updated - Id: {Id}, EmployeeId: {EmployeeId}, MonthYear: {MonthYear}",
			id, salarySlip.EmployeeId, salarySlip.MonthYear);

		return dto;
	}

	[UnitOfWork(true)]
	public virtual async Task<SalarySlipDto> FinalizeAsync(Guid id)
	{
		logger.LogInformation("FinalizeSalarySlip request - Id: {Id}", id);
		await revisions.LockAsync();

		var salarySlip = await salarySlipRepository.GetAsync(id);
		await accessService.EnsureEmployeeScopeAllowedAsync(salarySlip.EmployeeId);

		salarySlip = await draftFreshnessService.EnsureFreshAsync(salarySlip);
		var enrollments = await enrollmentResolver.ResolveAsync(
			new DateTime(salarySlip.Year, salarySlip.Month, 1), [salarySlip.EmployeeId]);
		enrollments.TryGetValue(salarySlip.EmployeeId, out var enrollment);
		EsiPayrollReadiness.EnsureInsuranceNumber(salarySlip.EmployeeStateInsuranceRuleId,
			salarySlip.EmployeeStateInsuranceCoverageDecision, enrollment?.InsuranceNumber);
		salarySlip.FinalizeSlip();

		await salarySlipRepository.UpdateAsync(salarySlip, autoSave: true);
		await continuityInvalidator.InvalidateAsync(salarySlip.MonthYear, [salarySlip.EmployeeId]);

		var employee = await employeeRepository.GetAsync(salarySlip.EmployeeId, includeDetails: false);
		var dto = await salarySlipDtoMapper.ToDtoAsync(salarySlip, employee);

		logger.LogInformation("Salary slip finalized - Id: {Id}, EmployeeId: {EmployeeId}, MonthYear: {MonthYear}",
			id, salarySlip.EmployeeId, salarySlip.MonthYear);

		return dto;
	}

	public async Task DeleteAsync(Guid id)
	{
		logger.LogInformation("DeleteSalarySlip request - Id: {Id}", id);

		var salarySlip = await salarySlipRepository.GetAsync(id);
		await accessService.EnsureEmployeeScopeAllowedAsync(salarySlip.EmployeeId);

		if (salarySlip.Status is SalarySlipStatus.Finalized or SalarySlipStatus.Paid)
		{
			throw new BusinessException(HRErrorCodes.SalarySlipAlreadyFinalized);
		}

		await salarySlipRepository.DeleteAsync(id);

		logger.LogInformation("Salary slip deleted - Id: {Id}, EmployeeId: {EmployeeId}, MonthYear: {MonthYear}",
			id, salarySlip.EmployeeId, salarySlip.MonthYear);
	}
}
