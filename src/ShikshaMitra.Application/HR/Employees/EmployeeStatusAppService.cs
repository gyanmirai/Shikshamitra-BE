using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.EventBus.Distributed;

namespace ShikshaMitra.HR.Employees;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
public class EmployeeStatusAppService(
	IEmployeeRepository employeeRepository,
	IEmployeeAuthorizationService employeeAuthorizationService,
	IEmployeeCacheManager employeeCacheManager,
	EmployeeManager employeeManager,
	EmployeeStatusTransitionValidator statusTransitionValidator,
	IDistributedEventBus distributedEventBus)
	: ApplicationService, IEmployeeStatusAppService
{
	public async Task<EmployeeStatusResponseDto> ChangeStatusAsync(Guid employeeId, ChangeEmployeeStatusDto input)
	{
		await employeeAuthorizationService.CheckEmployeeModifyAccessAsync(employeeId);

		var employee = await employeeRepository.GetAsync(employeeId)
			?? throw new EntityNotFoundException(typeof(Employee), employeeId);

		if (employee.EmployeeStatusType == input.EmployeeStatusType)
		{
			return new EmployeeStatusResponseDto
			{
				EmployeeId = employee.Id.ToString(),
				PreviousStatus = employee.EmployeeStatusType,
				CurrentStatus = input.EmployeeStatusType,
				UpdatedAt = employee.StatusLastChangedAt ?? DateTimeOffset.UtcNow
			};
		}

		statusTransitionValidator.Validate(
			employee.EmployeeStatusType,
			input.EmployeeStatusType,
			input.Remarks);

		var previousStatus = employee.EmployeeStatusType;

		employee = await employeeManager.UpdateEmployeeStatusAsync(
			employeeId,
			input.EmployeeStatusType,
			input.Remarks);

		await employeeRepository.UpdateAsync(employee);

		await employeeCacheManager.InvalidateEmployeeAsync(employeeId);

		await distributedEventBus.PublishAsync(new HR.Events.EmployeeStatusChangedEto
		{
			EmployeeId = employee.Id,
			PreviousStatus = previousStatus?.ToString(),
			CurrentStatus = input.EmployeeStatusType.ToString(),
			ChangeReason = input.Remarks,
			TenantId = CurrentTenant.Id,
			ChangedAt = DateTime.UtcNow
		});

		return new EmployeeStatusResponseDto
		{
			EmployeeId = employee.Id.ToString(),
			PreviousStatus = previousStatus,
			CurrentStatus = input.EmployeeStatusType,
			UpdatedAt = employee.StatusLastChangedAt ?? DateTimeOffset.UtcNow
		};
	}
}
