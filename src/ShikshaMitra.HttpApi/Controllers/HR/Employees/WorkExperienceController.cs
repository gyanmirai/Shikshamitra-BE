using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Employees.WorkExperienceContracts;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Employees;

/// <summary>
/// Employee work experience management controller
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/employees")]
[Authorize]
public class WorkExperienceController : AbpControllerBase
{
	private readonly IEmployeeWorkExperienceAppService _workExperienceAppService;

	public WorkExperienceController(IEmployeeWorkExperienceAppService workExperienceAppService)
	{
		_workExperienceAppService = workExperienceAppService;
	}

	/// <summary>
	/// Get employee work experience
	/// </summary>
	[HttpGet("{employeeId}/work-experience")]
	[ProducesResponseType(typeof(List<WorkExperienceContract>), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<List<WorkExperienceContract>> GetAsync([FromRoute, Required] Guid employeeId)
	{
		return await _workExperienceAppService.GetAsync(employeeId);
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
		await _workExperienceAppService.UpsertAsync(employeeId, request);
		return Ok();
	}
}
