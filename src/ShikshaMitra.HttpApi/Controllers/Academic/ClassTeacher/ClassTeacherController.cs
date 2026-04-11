using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.ClassTeacher;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.ClassTeacher;

[Route("api/class-teacher")]
public class ClassTeacherController(IClassTeacherAppService classTeacherAppService) : AbpController
{
	[HttpPut("assign")]
	public async Task<IActionResult> AssignOrUpdateTeachers(
		[FromQuery] Guid educationMediumId,
		[FromBody] List<AssignClassTeacherDto> input
	)
	{
		if (educationMediumId == Guid.Empty)
		{
			return BadRequest("educationMediumId is required.");
		}

		await classTeacherAppService.AssignOrUpdateTeachersAsync(input, educationMediumId);
		return Ok("Assignments processed successfully.");
	}

	[HttpGet("all")]
	public async Task<IActionResult> GetAllClassTeachers([FromQuery] Guid educationMediumId)
	{
		if (educationMediumId == Guid.Empty)
		{
			return BadRequest("educationMediumId is required.");
		}

		var classTeachers = await classTeacherAppService.GetAllClassTeachersAsync(educationMediumId);
		return Ok(classTeachers);
	}
}
