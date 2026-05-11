using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.ClassManagement;
using ShikshaMitra.Academic.ClassManagement.TeacherAssignment;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.ClassManagement;

[Authorize]
[ApiController]
public class TeacherAssignmentController(
	ITeacherAssignmentAppService appService
) : AbpController
{
	[HttpGet]
	[Route("api/class-management/teacher-assignments")]
	[ProducesResponseType(typeof(TeacherAssignmentGridResponseDto), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	[ProducesResponseType(404)]
	public async Task<IActionResult> GetTeacherAssignmentsForClassAsync(
		[FromQuery] Guid academicMediumId,
		[FromQuery] Guid classId
	)
	{
		if (academicMediumId == Guid.Empty)
		{
			return BadRequest(new { error = "academicMediumId is required" });
		}

		if (classId == Guid.Empty)
		{
			return BadRequest(new { error = "classId is required" });
		}

		var result = await appService.GetTeacherAssignmentsForClassAsync(
			academicMediumId,
			classId
		);

		return Ok(result);
	}

	[HttpPut]
	[Route("api/class-management/teacher-assignments")]
	[ProducesResponseType(typeof(ApiSuccessResponseDto), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	public async Task<IActionResult> SaveTeacherAssignmentsForClassAsync(
		[FromBody] SaveTeacherAssignmentsDto input
	)
	{
		var result = await appService.SaveTeacherAssignmentsForClassAsync(input);
		return Ok(result);
	}

	[HttpGet]
	[Route("api/class-management/teachers")]
	[ProducesResponseType(typeof(List<TeacherInfoDto>), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	public async Task<IActionResult> GetTeachersAsync(
		[FromQuery] string? search = null
	)
	{
		var result = await appService.GetTeachersAsync(search);
		return Ok(result);
	}
}
