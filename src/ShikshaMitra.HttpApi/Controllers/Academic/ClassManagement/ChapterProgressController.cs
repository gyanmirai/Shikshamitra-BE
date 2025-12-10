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
	[Route("api/subjects")]
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
	[Route("api/chapters/progress")]
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
	[Route("api/chapters/{chapterId}/progress")]
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
}
