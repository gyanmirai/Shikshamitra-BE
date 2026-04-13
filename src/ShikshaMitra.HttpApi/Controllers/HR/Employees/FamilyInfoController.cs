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
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/employees")]
[Authorize]
public class FamilyInfoController : AbpControllerBase
{
	private readonly IEmployeeFamilyInfoAppService _familyInfoAppService;
	private readonly IEmployeeAuthorizationService _authorizationService;

	public FamilyInfoController(
		IEmployeeFamilyInfoAppService familyInfoAppService,
		IEmployeeAuthorizationService authorizationService)
	{
		_familyInfoAppService = familyInfoAppService;
		_authorizationService = authorizationService;
	}

	/// <summary>
	/// Create or update employee family information
	/// </summary>
	[HttpPut("{employeeId}/family-info")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<IActionResult> UpdateAsync(
		[FromRoute, Required] Guid employeeId,
		[FromBody, Required] FamilyInfoRequest request)
	{
		await _authorizationService.CheckEmployeeModifyAccessAsync(employeeId);
		await _familyInfoAppService.UpsertAsync(employeeId, request);
		return NoContent();
	}

	/// <summary>
	/// Get employee family information
	/// </summary>
	[HttpGet("{employeeId}/family-info")]
	[ProducesResponseType(typeof(FamilyInfoResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<FamilyInfoResponse> GetAsync([FromRoute, Required] Guid employeeId)
	{
		await _authorizationService.CheckEmployeeAccessAsync(employeeId);
		return await _familyInfoAppService.GetAsync(employeeId);
	}
}
