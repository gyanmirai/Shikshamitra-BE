using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Employees.WorkExperience;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Employee work experience management controller
/// Handles CRUD operations for employee work history in the school ERP system
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/employees")]
[Authorize]
public class WorkExperienceController : AbpControllerBase
{
	private readonly IEmployeeAppService _employeeAppService;

	public WorkExperienceController(IEmployeeAppService employeeAppService)
	{
		_employeeAppService = employeeAppService;
	}

	/// <summary>
	/// Get employee work experience
	/// </summary>
	[HttpGet("{employeeId}/work-experience")]
	[ProducesResponseType(typeof(List<WorkExperienceContract>), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<List<WorkExperienceContract>> GetAsync([FromRoute, Required] Guid employeeId)
	{
		var employee = await _employeeAppService.GetAsync(employeeId);
		return WorkExperienceContractMapper.ToResponseList(employee.WorkExperiences);
	}

	/// <summary>
	/// Update employee work experience
	/// </summary>
	[HttpPut("{employeeId}/work-experience")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<IActionResult> UpdateAsync(
		[FromRoute, Required] Guid employeeId,
		[FromBody, Required] List<WorkExperienceContract> request)
	{
		var updateDtoList = WorkExperienceContractMapper.ToUpdateDtoList(request);
		await _employeeAppService.UpdateExperienceInfo(employeeId, updateDtoList);
		
		return Ok();
	}
}
