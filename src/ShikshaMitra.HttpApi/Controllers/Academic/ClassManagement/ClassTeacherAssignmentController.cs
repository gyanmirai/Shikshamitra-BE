using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.ClassManagement;
using ShikshaMitra.Academic.ClassManagement.ClassTeacherAssignment;
using ShikshaMitra.Constants.Permissions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.ClassManagement;

[Route("api/class-management/class-teacher-assignments")]
[Authorize]
[ApiController]
public class ClassTeacherAssignmentController(
	IClassTeacherAssignmentAppService appService
) : AbpController
{
	[HttpGet("workspace")]
	[Authorize(ShikshaMitraSubmodulePermissions.ClassManagement.ClassTeacherAssignment.View)]
	[ProducesResponseType(typeof(ClassTeacherWorkspaceDto), 200)]
	public async Task<IActionResult> GetWorkspaceAsync([FromQuery] Guid organizationId)
	{
		if (organizationId == Guid.Empty) return BadRequest(new { error = "organizationId is required" });
		return Ok(await appService.GetWorkspaceAsync(organizationId));
	}

	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.ClassManagement.ClassTeacherAssignment.View)]
	[ProducesResponseType(typeof(List<ClassTeacherRowDto>), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	public async Task<IActionResult> GetClassTeacherAssignmentsAsync(
		[FromQuery] Guid organizationId
	)
	{
		if (organizationId == Guid.Empty)
		{
			return BadRequest(new { error = "organizationId is required" });
		}

		var result = await appService.GetClassTeacherAssignmentsAsync(organizationId);
		return Ok(result);
	}

	[HttpGet("summary")]
	[Authorize(ShikshaMitraSubmodulePermissions.ClassManagement.ClassTeacherAssignment.View)]
	[ProducesResponseType(typeof(ClassTeacherSummaryDto), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	public async Task<IActionResult> GetClassTeacherAssignmentSummaryAsync(
		[FromQuery] Guid organizationId
	)
	{
		if (organizationId == Guid.Empty)
		{
			return BadRequest(new { error = "organizationId is required" });
		}

		var result = await appService.GetClassTeacherAssignmentSummaryAsync(organizationId);
		return Ok(result);
	}

	[HttpGet("eligible-teachers")]
	[Authorize(ShikshaMitraSubmodulePermissions.ClassManagement.ClassTeacherAssignment.View)]
	[ProducesResponseType(typeof(List<ClassTeacherEligibleTeacherDto>), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	public async Task<IActionResult> GetEligibleTeachersAsync(
		[FromQuery] Guid organizationId,
		[FromQuery] Guid? classId,
		[FromQuery] Guid? divisionId,
		[FromQuery] string? search
	)
	{
		if (organizationId == Guid.Empty)
		{
			return BadRequest(new { error = "organizationId is required" });
		}

		var result = await appService.GetEligibleTeachersAsync(
			organizationId,
			classId,
			divisionId,
			search
		);
		return Ok(result);
	}

	[HttpPost("validate")]
	[Authorize(ShikshaMitraSubmodulePermissions.ClassManagement.ClassTeacherAssignment.Add)]
	[ProducesResponseType(typeof(ClassTeacherValidationResultDto), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	public async Task<IActionResult> ValidateClassTeacherAssignmentsAsync(
		[FromBody] SaveClassTeacherAssignmentsDto input
	)
	{
		var result = await appService.ValidateClassTeacherAssignmentsAsync(input);
		return Ok(result);
	}

	[HttpGet("history")]
	[Authorize(ShikshaMitraSubmodulePermissions.ClassManagement.ClassTeacherAssignment.View)]
	[ProducesResponseType(typeof(List<ClassTeacherAssignmentHistoryDto>), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	public async Task<IActionResult> GetClassTeacherAssignmentHistoryAsync(
		[FromQuery] Guid organizationId
	)
	{
		if (organizationId == Guid.Empty)
		{
			return BadRequest(new { error = "organizationId is required" });
		}

		var result = await appService.GetClassTeacherAssignmentHistoryAsync(organizationId);
		return Ok(result);
	}

	[HttpPut]
	[Authorize(ShikshaMitraSubmodulePermissions.ClassManagement.ClassTeacherAssignment.Add)]
	[ProducesResponseType(typeof(ApiSuccessResponseDto), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	public async Task<IActionResult> SaveClassTeacherAssignmentsAsync(
		[FromBody] SaveClassTeacherAssignmentsDto input
	)
	{
		var result = await appService.SaveClassTeacherAssignmentsAsync(input);
		return Ok(result);
	}
}
