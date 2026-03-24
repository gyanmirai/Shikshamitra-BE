using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.PersonalInfo;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Employees;

/// <summary>
/// Employee personal information management controller
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/employees")]
[Authorize]
public class PersonalInfoController : AbpControllerBase
{
	private readonly IEmployeePersonalInfoAppService _personalInfoAppService;
	private readonly IEmployeeAuthorizationService _authorizationService;

	public PersonalInfoController(
		IEmployeePersonalInfoAppService personalInfoAppService,
		IEmployeeAuthorizationService authorizationService)
	{
		_personalInfoAppService = personalInfoAppService;
		_authorizationService = authorizationService;
	}

	/// <summary>
	/// Create employee personal information
	/// </summary>
	[HttpPost("personal-info")]
	[ProducesResponseType(typeof(PersonalInfoResponse), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public virtual async Task<PersonalInfoResponse> CreateAsync(
		[FromBody, Required] PersonalInfoRequest request)
	{
		var createDto = PersonalInfoContractMapper.ToCreateDto(request);
		var result = await _personalInfoAppService.CreateAsync(createDto);
		return PersonalInfoContractMapper.ToResponse(result);
	}

	/// <summary>
	/// Get employee personal information
	/// </summary>
	[HttpGet("{employeeId}/personal-info")]
	[ProducesResponseType(typeof(PersonalInfoResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<PersonalInfoResponse> GetAsync([FromRoute, Required] Guid employeeId)
	{
		await _authorizationService.CheckEmployeeAccessAsync(employeeId);
		var result = await _personalInfoAppService.GetAsync(employeeId);
		return PersonalInfoContractMapper.ToResponse(result);
	}

	/// <summary>
	/// Update employee personal information
	/// </summary>
	[HttpPut("{employeeId}/personal-info")]
	[ProducesResponseType(typeof(PersonalInfoResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<PersonalInfoResponse> UpdateAsync(
		[FromRoute, Required] Guid employeeId,
		[FromBody, Required] PersonalInfoRequest request)
	{
		await _authorizationService.CheckEmployeeModifyAccessAsync(employeeId);
		var updateDto = PersonalInfoContractMapper.ToUpdateDto(request);
		var result = await _personalInfoAppService.UpdateAsync(employeeId, updateDto);
		return PersonalInfoContractMapper.ToResponse(result);
	}
}
