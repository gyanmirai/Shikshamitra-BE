using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.AdditionalInfo;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Employees;

/// <summary>
/// Employee additional information management controller
/// Handles CRUD operations for employee additional data in the school ERP system
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/employees")]
[Authorize]
public class AdditionalInfoController : AbpControllerBase
{
	private readonly IEmployeeAppService _employeeAppService;

	public AdditionalInfoController(IEmployeeAppService employeeAppService)
	{
		_employeeAppService = employeeAppService;
	}

	/// <summary>
	/// Get employee additional information
	/// </summary>
	[HttpGet("{employeeId}/additional-info")]
	[ProducesResponseType(typeof(AdditionalInfoContract), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<AdditionalInfoContract> GetAsync([FromRoute, Required] Guid employeeId)
	{
		var additionalDetails = await _employeeAppService.GetAdditionalDetailsAsync(employeeId);
		return AdditionalInfoContractMapper.ToResponse(additionalDetails);
	}

	/// <summary>
	/// Update employee additional information
	/// </summary>
	[HttpPut("{employeeId}/additional-info")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<IActionResult> UpdateAsync(
		[FromRoute, Required] Guid employeeId,
		[FromBody, Required] AdditionalInfoContract request)
	{
		var updateDto = AdditionalInfoContractMapper.ToUpdateDto(request);
		await _employeeAppService.UpdateAdditionalDetailsAsync(employeeId, updateDto);
		
		return Ok();
	}
}
