using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using System;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.EventBus.Distributed;

namespace ShikshaMitra.HR.Employees;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
public class EmployeeStatusAppService(
	IEmployeeRepository employeeRepository,
	EmployeeManager employeeManager,
	IDistributedEventBus distributedEventBus)
	: ApplicationService, IEmployeeStatusAppService
{
	public async Task<EmployeeStatusResponseDto> ChangeStatusAsync(Guid employeeId, ChangeEmployeeStatusDto input)
	{
		var employee = await employeeRepository.GetAsync(employeeId)
			?? throw new EntityNotFoundException(typeof(Employee), employeeId);

		var previousStatus = employee.EmployeeStatusType;

		employee = await employeeManager.UpdateEmployeeStatusAsync(
			employeeId, input.EmployeeStatusType, null);

		await employeeRepository.UpdateAsync(employee);

		await distributedEventBus.PublishAsync(new HR.Events.EmployeeStatusChangedEto
		{
			EmployeeId = employee.Id,
			PreviousStatus = previousStatus?.ToString(),
			CurrentStatus = input.EmployeeStatusType.ToString(),
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

