using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Extensions;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.ClassTeacher;

[Route("api/class-teacher")]
public class ClassTeacherController(IClassTeacherAppService classTeacherAppService) : AbpController
{
	[HttpPut("assign")]
	public async Task<IActionResult> AssignOrUpdateTeachers(
		[FromBody] List<AssignClassTeacherDto> input
	)
	{
		var headers = this.Headers();
		await classTeacherAppService.AssignOrUpdateTeachersAsync(input, headers.EducationMediumId);
		return Ok("Assignments processed successfully.");
	}

	[HttpGet("all")]
	public async Task<IActionResult> GetAllClassTeachers()
	{
		var headers = this.Headers();
		var classTeachers = await classTeacherAppService.GetAllClassTeachersAsync(
			headers.EducationMediumId
		);
		return Ok(classTeachers);
	}
}
