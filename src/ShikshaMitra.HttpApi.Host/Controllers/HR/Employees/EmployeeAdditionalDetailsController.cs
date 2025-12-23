using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees.AdditionalDetails;
using System;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Employees;

/// <summary>
/// Controller for managing employee additional details.
/// </summary>
[Route("api/employees/{employeeId}/additional-details")]
[Authorize]
public class EmployeeAdditionalDetailsController(
	IEmployeeAdditionalDetailsAppService additionalDetailsAppService)
	: AbpController
{
	/// <summary>
	/// Get employee additional details.
	/// </summary>
	/// <param name="employeeId">The employee ID.</param>
	/// <returns>Employee additional details record.</returns>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_AdditionalDetails.View)]
	[ProducesResponseType(typeof(EmployeeAdditionalDetailsDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	public async Task<EmployeeAdditionalDetailsDto> GetAsync(Guid employeeId)
	{
		return await additionalDetailsAppService.GetAsync(employeeId);
	}

	/// <summary>
	/// Upsert employee additional details.
	/// Creates the additional details record if it doesn't exist, otherwise updates it.
	/// </summary>
	/// <param name="employeeId">The employee ID.</param>
	/// <param name="input">The additional details to upsert.</param>
	/// <returns>The created or updated additional details.</returns>
	[HttpPut]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_AdditionalDetails.Update)]
	[ProducesResponseType(typeof(EmployeeAdditionalDetailsDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(EmployeeAdditionalDetailsDto), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status409Conflict)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	public async Task<IActionResult> UpsertAsync(Guid employeeId, [FromBody] EmployeeAdditionalDetailsUpsertDto input)
	{
		var (result, isCreated) = await additionalDetailsAppService.UpsertAsync(employeeId, input);

		if (isCreated)
		{
			return CreatedAtAction(
				nameof(GetAsync),
				new { employeeId },
				result);
		}

		return Ok(result);
	}

	/// <summary>
	/// Partially update employee additional details.
	/// Only provided fields are updated.
	/// </summary>
	/// <param name="employeeId">The employee ID.</param>
	/// <param name="input">The fields to update.</param>
	/// <returns>The updated additional details.</returns>
	[HttpPatch]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_AdditionalDetails.Update)]
	[ProducesResponseType(typeof(EmployeeAdditionalDetailsDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status409Conflict)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	public async Task<EmployeeAdditionalDetailsDto> PatchAsync(
		Guid employeeId,
		[FromBody] EmployeeAdditionalDetailsPatchDto input)
	{
		return await additionalDetailsAppService.PatchAsync(employeeId, input);
	}
}



