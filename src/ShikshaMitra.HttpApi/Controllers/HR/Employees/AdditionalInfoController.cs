using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.AdditionalDetails;
using ShikshaMitra.HR.Employees.AdditionalInfo;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Employees;

/// <summary>
/// Employee additional information management controller
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/employees")]
[Authorize]
public class AdditionalInfoController : AbpControllerBase
{
	private readonly IEmployeeAdditionalDetailsAppService _additionalDetailsAppService;
	private readonly IEmployeeAuthorizationService _authorizationService;

	public AdditionalInfoController(
		IEmployeeAdditionalDetailsAppService additionalDetailsAppService,
		IEmployeeAuthorizationService authorizationService)
	{
		_additionalDetailsAppService = additionalDetailsAppService;
		_authorizationService = authorizationService;
	}

	/// <summary>
	/// Get employee additional information
	/// </summary>
	[HttpGet("{employeeId}/additional-info")]
	[ProducesResponseType(typeof(AdditionalInfoContract), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<AdditionalInfoContract> GetAsync([FromRoute, Required] Guid employeeId)
	{
		await _authorizationService.CheckEmployeeAccessAsync(employeeId);
		var dto = await _additionalDetailsAppService.GetAsync(employeeId);
		return new AdditionalInfoContract
		{
			BplCardNo = dto.BplCardNo,
			PersonWithDisability = dto.PersonWithDisability,
			SecondLanguage = dto.SecondLanguage,
			IsLibraryMember = dto.LibraryMember,
			IsTransportMember = dto.TransportMember
		};
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
		await _authorizationService.CheckEmployeeModifyAccessAsync(employeeId);
		var upsertDto = new EmployeeAdditionalDetailsUpsertDto
		{
			BplCardNo = request.BplCardNo,
			PersonWithDisability = request.PersonWithDisability,
			SecondLanguage = request.SecondLanguage,
			LibraryMember = request.IsLibraryMember,
			TransportMember = request.IsTransportMember
		};
		await _additionalDetailsAppService.UpsertAsync(employeeId, upsertDto);
		return Ok();
	}
}
