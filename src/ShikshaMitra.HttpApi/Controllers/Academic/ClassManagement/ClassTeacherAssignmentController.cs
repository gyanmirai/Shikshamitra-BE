using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.ClassManagement;
using ShikshaMitra.Academic.ClassManagement.ClassTeacherAssignment;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.ClassManagement;

[Route("api/class-teachers/assignments")]
[Authorize]
[ApiController]
public class ClassTeacherAssignmentController(
	IClassTeacherAssignmentAppService appService
) : AbpController
{
	[HttpGet]
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

	[HttpPut]
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
