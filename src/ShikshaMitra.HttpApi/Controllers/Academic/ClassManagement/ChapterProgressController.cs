using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.ClassManagement;
using ShikshaMitra.Academic.ClassManagement.ChapterProgress;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.ClassManagement;

[Authorize]
[ApiController]
public class ChapterProgressController(
	IChapterProgressAppService appService
) : AbpController
{
	[HttpGet]
	[Route("api/class-management/chapter-progress/subjects")]
	[ProducesResponseType(typeof(List<ClassDivisionSubjectDto>), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	public async Task<IActionResult> GetSubjectsForClassDivisionAsync(
		[FromQuery] Guid academicMediumId,
		[FromQuery] Guid classId,
		[FromQuery] Guid divisionId
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

		if (divisionId == Guid.Empty)
		{
			return BadRequest(new { error = "divisionId is required" });
		}

		var result = await appService.GetSubjectsForClassDivisionAsync(
			academicMediumId,
			classId,
			divisionId
		);

		return Ok(result);
	}

	[HttpGet]
	[Route("api/class-management/chapter-progress")]
	[ProducesResponseType(typeof(PaginatedChapterProgressResponseDto), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	[ProducesResponseType(404)]
	public async Task<IActionResult> GetChapterProgressListAsync(
		[FromQuery] Guid academicMediumId,
		[FromQuery] Guid classId,
		[FromQuery] Guid divisionId,
		[FromQuery] Guid subjectId,
		[FromQuery] string? search = null
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

		if (divisionId == Guid.Empty)
		{
			return BadRequest(new { error = "divisionId is required" });
		}

		if (subjectId == Guid.Empty)
		{
			return BadRequest(new { error = "subjectId is required" });
		}

		var result = await appService.GetChapterProgressListAsync(
			academicMediumId,
			classId,
			divisionId,
			subjectId,
			search
		);

		return Ok(result);
	}

	[HttpPut]
	[Route("api/class-management/chapters/{chapterId}/progress")]
	[ProducesResponseType(typeof(ApiSuccessResponseDto), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	[ProducesResponseType(404)]
	public async Task<IActionResult> UpdateChapterProgressAsync(
		[FromRoute] Guid chapterId,
		[FromBody] UpdateChapterProgressDto input
	)
	{
		if (chapterId == Guid.Empty)
		{
			return BadRequest(new { error = "chapterId is required" });
		}

		var result = await appService.UpdateChapterProgressAsync(chapterId, input);
		return Ok(result);
	}

	[HttpGet]
	[Route("api/chapters/subjects")]
	[ProducesResponseType(typeof(List<SubjectForChaptersDto>), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	public async Task<IActionResult> GetSubjectsForStandardAsync([FromQuery] Guid standardId)
	{
		if (standardId == Guid.Empty)
		{
			return BadRequest(new { error = "standardId is required" });
		}

		var result = await appService.GetSubjectsForStandardAsync(standardId);
		return Ok(result);
	}

	[HttpGet]
	[Route("api/class-management/chapters/{chapterId}/progress/logs")]
	[ProducesResponseType(typeof(List<ChapterProgressLogDto>), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	public async Task<IActionResult> GetChapterProgressLogsAsync(
		[FromRoute] Guid chapterId,
		[FromQuery] Guid divisionId
	)
	{
		if (chapterId == Guid.Empty)
		{
			return BadRequest(new { error = "chapterId is required" });
		}

		if (divisionId == Guid.Empty)
		{
			return BadRequest(new { error = "divisionId is required" });
		}

		var result = await appService.GetChapterProgressLogsAsync(chapterId, divisionId);
		return Ok(result);
	}

	[HttpGet]
	[Route("api/class-management/chapter-progress/employee/{employeeId}/classes")]
	[ProducesResponseType(typeof(List<EmployeeClassDto>), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	public async Task<IActionResult> GetClassesByEmployeeAsync([FromRoute] Guid employeeId)
	{
		if (employeeId == Guid.Empty)
		{
			return BadRequest(new { error = "employeeId is required" });
		}

		var result = await appService.GetClassesByEmployeeAsync(employeeId);
		return Ok(result);
	}

	[HttpGet]
	[Route("api/class-management/chapter-progress/employee/{employeeId}/divisions")]
	[ProducesResponseType(typeof(List<EmployeeDivisionDto>), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	public async Task<IActionResult> GetDivisionsByEmployeeAndClassAsync(
		[FromRoute] Guid employeeId,
		[FromQuery] Guid classId
	)
	{
		if (employeeId == Guid.Empty)
		{
			return BadRequest(new { error = "employeeId is required" });
		}

		if (classId == Guid.Empty)
		{
			return BadRequest(new { error = "classId is required" });
		}

		var result = await appService.GetDivisionsByEmployeeAndClassAsync(employeeId, classId);
		return Ok(result);
	}

	[HttpGet]
	[Route("api/class-management/chapter-progress/employee/{employeeId}/subjects-progress")]
	[ProducesResponseType(typeof(List<ClassDivisionSubjectDto>), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	public async Task<IActionResult> GetSubjectsProgressByEmployeeAsync(
		[FromRoute] Guid employeeId,
		[FromQuery] Guid classId,
		[FromQuery] Guid divisionId
	)
	{
		if (employeeId == Guid.Empty)
		{
			return BadRequest(new { error = "employeeId is required" });
		}

		if (classId == Guid.Empty)
		{
			return BadRequest(new { error = "classId is required" });
		}

		if (divisionId == Guid.Empty)
		{
			return BadRequest(new { error = "divisionId is required" });
		}

		var result = await appService.GetSubjectsProgressByEmployeeAsync(employeeId, classId, divisionId);
		return Ok(result);
	}
}
