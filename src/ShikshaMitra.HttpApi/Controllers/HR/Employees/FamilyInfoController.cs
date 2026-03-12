using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.FamilyInfo;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Employees;

/// <summary>
/// Employee family information management controller
/// Handles CRUD operations for employee family data in the school ERP system
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/employees")]
[Authorize]
public class FamilyInfoController : AbpControllerBase
{
	private readonly IEmployeeAppService _employeeAppService;
	private readonly IEmployeeAuthorizationService _authorizationService;

	public FamilyInfoController(
		IEmployeeAppService employeeAppService,
		IEmployeeAuthorizationService authorizationService)
	{
		_employeeAppService = employeeAppService;
		_authorizationService = authorizationService;
	}

	/// <summary>
	/// Create or update employee family information
	/// </summary>
	[HttpPut("{employeeId}/family-info")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	public virtual async Task<IActionResult> UpdateAsync(
		[FromRoute, Required] Guid employeeId,
		[FromBody, Required] FamilyInfoRequest request)
	{
		var updateDto = FamilyInfoContractMapper.ToUpdateDto(request);
		await _employeeAppService.UpdateFamilyInfo(employeeId, updateDto);

		return NoContent();
	}

	/// <summary>
	/// Get employee family information
	/// </summary>
	[HttpGet("{employeeId}/family-info")]
	[ProducesResponseType(typeof(FamilyInfoResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	public virtual async Task<FamilyInfoResponse> GetAsync([FromRoute, Required] Guid employeeId)
	{
		// Check access rights (IDOR protection)
		await _authorizationService.CheckEmployeeAccessAsync(employeeId);

		var employee = await _employeeAppService.GetAsync(employeeId);

		return FamilyInfoContractMapper.ToResponse(
			employeeId,
			employee.FatherDetails,
			employee.MotherDetails,
			employee.SpouseDetails);
	}
}
