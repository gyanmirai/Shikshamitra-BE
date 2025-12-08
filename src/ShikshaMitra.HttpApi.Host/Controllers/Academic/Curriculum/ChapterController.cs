using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Curriculum;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.Curriculum;

/// <summary>
/// Controller for Chapter Management operations
/// Aligned with FE contract: /api/chapters
/// </summary>
[Route("api/chapters")]
[Authorize]
[ApiController]
public class ChapterController(
	IChapterManagementAppService chapterManagementAppService
) : AbpController
{
	/// <summary>
	/// Creates a new chapter for a subject
	/// </summary>
	/// <param name="academicMediumId">Academic medium ID (required query parameter)</param>
	/// <param name="classId">Standard/Class ID (required query parameter)</param>
	/// <param name="subjectId">Subject ID (required query parameter)</param>
	/// <param name="input">Chapter details</param>
	/// <returns>Created chapter</returns>
	/// <response code="201">Chapter created successfully</response>
	/// <response code="400">Validation failed</response>
	/// <response code="401">Unauthorized</response>
	[HttpPost]
	[ProducesResponseType(typeof(ChapterResponseDto), 201)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	public async Task<IActionResult> CreateAsync(
		[FromQuery] Guid academicMediumId,
		[FromQuery] Guid classId,
		[FromQuery] Guid subjectId,
		[FromBody] CreateChapterDto input
	)
	{
		// Validate query parameters
		if (academicMediumId == Guid.Empty)
		{
			return BadRequest(new
			{
				error = "Validation failed",
				details = new { academicMediumId = "academicMediumId is required" }
			});
		}

		if (classId == Guid.Empty)
		{
			return BadRequest(new
			{
				error = "Validation failed",
				details = new { classId = "classId is required" }
			});
		}

		if (subjectId == Guid.Empty)
		{
			return BadRequest(new
			{
				error = "Validation failed",
				details = new { subjectId = "subjectId is required" }
			});
		}

		var result = await chapterManagementAppService.CreateAsync(
			academicMediumId,
			classId,
			subjectId,
			input
		);

		return StatusCode(201, new
		{
			message = "Chapter created successfully",
			data = result
		});
	}

	/// <summary>
	/// Gets all chapters for a specific subject
	/// </summary>
	/// <param name="academicMediumId">Academic medium ID (required)</param>
	/// <param name="classId">Standard/Class ID (required)</param>
	/// <param name="subjectId">Subject ID (required)</param>
	/// <returns>List of chapters ordered by chapter number</returns>
	/// <response code="200">Returns the list of chapters</response>
	/// <response code="400">Required parameters missing</response>
	[HttpGet]
	[ProducesResponseType(typeof(List<ChapterResponseDto>), 200)]
	[ProducesResponseType(400)]
	public async Task<IActionResult> GetListAsync(
		[FromQuery] Guid academicMediumId,
		[FromQuery] Guid classId,
		[FromQuery] Guid subjectId
	)
	{
		// Validate query parameters
		if (academicMediumId == Guid.Empty)
		{
			return BadRequest(new
			{
				error = "Validation failed",
				details = new { academicMediumId = "academicMediumId is required" }
			});
		}

		if (classId == Guid.Empty)
		{
			return BadRequest(new
			{
				error = "Validation failed",
				details = new { classId = "classId is required" }
			});
		}

		if (subjectId == Guid.Empty)
		{
			return BadRequest(new
			{
				error = "Validation failed",
				details = new { subjectId = "subjectId is required" }
			});
		}

		var result = await chapterManagementAppService.GetListAsync(
			academicMediumId,
			classId,
			subjectId
		);

		return Ok(new
		{
			data = result
		});
	}

	/// <summary>
	/// Gets a single chapter by ID
	/// </summary>
	/// <param name="chapterId">Chapter ID</param>
	/// <returns>Chapter details</returns>
	/// <response code="200">Returns the chapter</response>
	/// <response code="404">Chapter not found</response>
	[HttpGet("{chapterId}")]
	[ProducesResponseType(typeof(ChapterResponseDto), 200)]
	[ProducesResponseType(404)]
	public async Task<IActionResult> GetAsync([FromRoute] Guid chapterId)
	{
		var result = await chapterManagementAppService.GetAsync(chapterId);

		return Ok(new
		{
			data = result
		});
	}

	/// <summary>
	/// Updates an existing chapter
	/// </summary>
	/// <param name="chapterId">Chapter ID</param>
	/// <param name="academicMediumId">Academic medium ID (required query parameter)</param>
	/// <param name="classId">Standard/Class ID (required query parameter)</param>
	/// <param name="subjectId">Subject ID (required query parameter)</param>
	/// <param name="input">Updated chapter details</param>
	/// <returns>Updated chapter</returns>
	/// <response code="200">Chapter updated successfully</response>
	/// <response code="400">Validation failed</response>
	/// <response code="404">Chapter not found</response>
	[HttpPut("{chapterId}")]
	[ProducesResponseType(typeof(ChapterResponseDto), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(404)]
	public async Task<IActionResult> UpdateAsync(
		[FromRoute] Guid chapterId,
		[FromQuery] Guid academicMediumId,
		[FromQuery] Guid classId,
		[FromQuery] Guid subjectId,
		[FromBody] UpdateChapterDto input
	)
	{
		// Validate query parameters
		if (academicMediumId == Guid.Empty)
		{
			return BadRequest(new
			{
				error = "Validation failed",
				details = new { academicMediumId = "academicMediumId is required" }
			});
		}

		if (classId == Guid.Empty)
		{
			return BadRequest(new
			{
				error = "Validation failed",
				details = new { classId = "classId is required" }
			});
		}

		if (subjectId == Guid.Empty)
		{
			return BadRequest(new
			{
				error = "Validation failed",
				details = new { subjectId = "subjectId is required" }
			});
		}

		var result = await chapterManagementAppService.UpdateAsync(
			chapterId,
			academicMediumId,
			classId,
			subjectId,
			input
		);

		return Ok(new
		{
			message = "Chapter updated successfully",
			data = result
		});
	}

	/// <summary>
	/// Deletes a chapter and its documents
	/// </summary>
	/// <param name="chapterId">Chapter ID</param>
	/// <returns>No content on success</returns>
	/// <response code="200">Chapter deleted successfully</response>
	/// <response code="404">Chapter not found</response>
	[HttpDelete("{chapterId}")]
	[ProducesResponseType(200)]
	[ProducesResponseType(404)]
	public async Task<IActionResult> DeleteAsync([FromRoute] Guid chapterId)
	{
		await chapterManagementAppService.DeleteAsync(chapterId);

		return Ok(new
		{
			message = "Chapter deleted successfully"
		});
	}
}
