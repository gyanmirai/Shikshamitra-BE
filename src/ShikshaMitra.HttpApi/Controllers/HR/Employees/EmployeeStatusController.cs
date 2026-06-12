using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Status;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Employees;

/// <summary>
/// Employee employment status (Active / Inactive / Left / Retired).
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/employees")]
[Authorize]
public class EmployeeStatusController(IEmployeeStatusAppService employeeStatusAppService) : AbpControllerBase
{
	/// <summary>
	/// Check whether an employee can be moved to a non-active status.
	/// </summary>
	[HttpGet("{employeeId}/status-deactivation-check")]
	[ProducesResponseType(typeof(EmployeeStatusDeactivationCheckResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<EmployeeStatusDeactivationCheckResponse> GetDeactivationCheckAsync(
		[FromRoute, Required] Guid employeeId)
	{
		var result = await employeeStatusAppService.GetDeactivationCheckAsync(employeeId);
		return EmployeeStatusContractMapper.ToResponse(result);
	}

	/// <summary>
	/// Change employee status. Idempotent: same status returns 200 without persisting or publishing an event.
	/// </summary>
	[HttpPut("{employeeId}/status")]
	[ProducesResponseType(typeof(EmployeeStatusUpdateResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<EmployeeStatusUpdateResponse> UpdateStatusAsync(
		[FromRoute, Required] Guid employeeId,
		[FromBody, Required] EmployeeStatusUpdateRequest request)
	{
		var dto = EmployeeStatusContractMapper.ToChangeDto(request);
		var result = await employeeStatusAppService.ChangeStatusAsync(employeeId, dto);
		return EmployeeStatusContractMapper.ToResponse(result);
	}
}
