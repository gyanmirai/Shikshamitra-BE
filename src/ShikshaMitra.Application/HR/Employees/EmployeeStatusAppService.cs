using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.EventBus.Distributed;

namespace ShikshaMitra.HR.Employees;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Edit)]
public class EmployeeStatusAppService(
	IEmployeeRepository employeeRepository,
	IEmployeeAuthorizationService employeeAuthorizationService,
	EmployeeActivationReadinessService activationReadinessService,
	IEmployeeCacheManager employeeCacheManager,
	EmployeeManager employeeManager,
	EmployeeStatusTransitionValidator statusTransitionValidator,
	IDistributedEventBus distributedEventBus)
	: ApplicationService, IEmployeeStatusAppService
{
	public async Task<EmployeeActivationReadinessDto> GetActivationReadinessAsync(Guid employeeId)
	{
		await employeeAuthorizationService.CheckEmployeeModifyAccessAsync(employeeId);
		return await activationReadinessService.GetAsync(employeeId);
	}

	public async Task<EmployeeStatusDeactivationCheckDto> GetDeactivationCheckAsync(Guid employeeId)
	{
		await employeeAuthorizationService.CheckEmployeeModifyAccessAsync(employeeId);

		_ = await employeeRepository.GetAsync(employeeId)
			?? throw new EntityNotFoundException(typeof(Employee), employeeId);

		var activeDirectReportCount = await GetActiveDirectReportCountAsync(employeeId);

		return new EmployeeStatusDeactivationCheckDto
		{
			CanDeactivate = activeDirectReportCount == 0,
			ActiveDirectReportCount = activeDirectReportCount
		};
	}

	public async Task<EmployeeStatusResponseDto> ChangeStatusAsync(Guid employeeId, ChangeEmployeeStatusDto input)
	{
		await employeeAuthorizationService.CheckEmployeeModifyAccessAsync(employeeId);

		var employee = await employeeRepository.GetAsync(employeeId)
			?? throw new EntityNotFoundException(typeof(Employee), employeeId);

		var effectiveDate = NormalizeStatusEffectiveDate(input.EmployeeStatusType, input.EffectiveDate);

		if (employee.EmployeeStatusType == input.EmployeeStatusType &&
		    employee.StatusEffectiveDate == effectiveDate)
		{
			return EmployeeStatusProjection.ToResponse(
				employee,
				employee.EmployeeStatusType,
				input.EmployeeStatusType,
				new DateTimeOffset(Clock.Now));
		}

		statusTransitionValidator.Validate(
			employee.EmployeeStatusType,
			input.EmployeeStatusType,
			input.Remarks);

		if (input.EmployeeStatusType == EmployeeStatusType.Active)
		{
			await EnsurePayrollStructureExistsAsync(employee.Id);
		}
		else
		{
			await EnsureNoActiveDirectReportsAsync(employee.Id);
		}

		var previousStatus = employee.EmployeeStatusType;

		employee = await employeeManager.UpdateEmployeeStatusAsync(
			employeeId,
			input.EmployeeStatusType,
			input.Remarks,
			effectiveDate);

		await employeeRepository.UpdateAsync(employee);

		await employeeCacheManager.InvalidateEmployeeAsync(employeeId);

		await distributedEventBus.PublishAsync(new HR.Events.EmployeeStatusChangedEto
		{
			EmployeeId = employee.Id,
			PreviousStatus = previousStatus?.ToString(),
			CurrentStatus = input.EmployeeStatusType.ToString(),
			ChangeReason = input.Remarks,
			EffectiveDate = employee.StatusEffectiveDate,
			TenantId = CurrentTenant.Id,
			ChangedAt = Clock.Now
		});

		return EmployeeStatusProjection.ToResponse(
			employee,
			previousStatus,
			input.EmployeeStatusType,
			new DateTimeOffset(Clock.Now));
	}

	private static DateOnly? NormalizeStatusEffectiveDate(EmployeeStatusType status, DateOnly? effectiveDate)
	{
		if (status == EmployeeStatusType.Active)
		{
			return null;
		}

		return effectiveDate ?? throw new BusinessException(HRErrorCodes.EmployeeStatusEffectiveDateRequired)
			.WithData("field", nameof(ChangeEmployeeStatusDto.EffectiveDate))
			.WithData("requiredForStatus", status.ToString());
	}

	private async Task EnsurePayrollStructureExistsAsync(Guid employeeId)
	{
		var readiness = await activationReadinessService.GetAsync(employeeId);
		if (!readiness.CanActivate)
		{
			throw new BusinessException(HRErrorCodes.EmployeePayrollStructureRequired)
				.WithData("employeeId", employeeId)
				.WithData("requiredMonth", readiness.RequiredMonth.ToString("yyyy-MM"))
				.WithData("blockerCodes", readiness.BlockerCodes);
		}
	}

	private async Task EnsureNoActiveDirectReportsAsync(Guid employeeId)
	{
		var activeDirectReportCount = await GetActiveDirectReportCountAsync(employeeId);

		if (activeDirectReportCount > 0)
		{
			throw new BusinessException(HRErrorCodes.ReportingManagerHasActiveReports)
				.WithData("EmployeeId", employeeId)
				.WithData("ActiveDirectReportCount", activeDirectReportCount);
		}
	}

	private async Task<int> GetActiveDirectReportCountAsync(Guid employeeId)
	{
		var employeeQuery = await employeeRepository.GetQueryableAsync();
		return await AsyncExecuter.CountAsync(
			employeeQuery.Where(e =>
				e.ReportingManagerId == employeeId &&
				e.EmployeeStatusType == EmployeeStatusType.Active));
	}
}
