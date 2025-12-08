using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Curriculum;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.Curriculum;

/// <summary>
/// Controller for Subject Assignment operations
/// Aligned with FE contract: /api/subject-assignments
/// </summary>
[Route("api/subject-assignments")]
[Authorize]
[ApiController]
public class SubjectAssignmentController(
	ISubjectAssignmentAppService subjectAssignmentAppService
) : AbpController
{
	/// <summary>
	/// Creates a new subject assignment group (Draft or Published)
	/// </summary>
	/// <param name="academicMediumId">Academic medium ID (required query parameter)</param>
	/// <param name="input">Subject assignment group details</param>
	/// <returns>Created assignment group</returns>
	/// <response code="201">Assignment group created successfully</response>
	/// <response code="400">Validation failed</response>
	/// <response code="401">Unauthorized</response>
	[HttpPost]
	[ProducesResponseType(typeof(SubjectAssignmentGroupResponseDto), 201)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	public async Task<IActionResult> CreateAsync(
		[FromQuery] Guid academicMediumId,
		[FromBody] CreateSubjectAssignmentGroupDto input
	)
	{
		if (academicMediumId == Guid.Empty)
		{
			return BadRequest(new
			{
				error = "Validation failed",
				details = new { academicMediumId = "academicMediumId is required" }
			});
		}

		var result = await subjectAssignmentAppService.CreateAsync(academicMediumId, input);
		return StatusCode(201, result);
	}

	/// <summary>
	/// Gets all subject assignment groups with optional filters
	/// </summary>
	/// <param name="academicMediumId">Academic medium ID (required)</param>
	/// <param name="classId">Standard/Class ID (required)</param>
	/// <param name="status">Optional status filter: 0=Draft, 1=Published</param>
	/// <returns>List of assignment groups</returns>
	/// <response code="200">Returns the list of assignments</response>
	/// <response code="400">Required parameters missing</response>
	[HttpGet]
	[ProducesResponseType(typeof(List<SubjectAssignmentGroupResponseDto>), 200)]
	[ProducesResponseType(400)]
	public async Task<IActionResult> GetListAsync(
		[FromQuery] Guid academicMediumId,
		[FromQuery] Guid classId,
		[FromQuery] int? status = null
	)
	{
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

		if (status.HasValue && (status.Value < 0 || status.Value > 1))
		{
			return BadRequest(new
			{
				error = "Validation failed",
				details = new { status = "status must be 0 (Draft) or 1 (Published)" }
			});
		}

		var result = await subjectAssignmentAppService.GetListAsync(
			academicMediumId,
			classId,
			status
		);

		return Ok(result);
	}

	/// <summary>
	/// Gets a single subject assignment group by ID
	/// </summary>
	/// <param name="assignmentId">Assignment group ID</param>
	/// <returns>Assignment group details</returns>
	/// <response code="200">Returns the assignment group</response>
	/// <response code="404">Assignment not found</response>
	[HttpGet("{assignmentId}")]
	[ProducesResponseType(typeof(SubjectAssignmentGroupResponseDto), 200)]
	[ProducesResponseType(404)]
	public async Task<IActionResult> GetAsync([FromRoute] Guid assignmentId)
	{
		var result = await subjectAssignmentAppService.GetAsync(assignmentId);
		return Ok(result);
	}

	/// <summary>
	/// Updates an existing subject assignment group
	/// </summary>
	/// <param name="assignmentId">Assignment group ID</param>
	/// <param name="academicMediumId">Academic medium ID (required query parameter)</param>
	/// <param name="input">Updated assignment details</param>
	/// <returns>Updated assignment group</returns>
	/// <response code="200">Assignment updated successfully</response>
	/// <response code="400">Validation failed</response>
	/// <response code="404">Assignment not found</response>
	[HttpPut("{assignmentId}")]
	[ProducesResponseType(typeof(SubjectAssignmentGroupResponseDto), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(404)]
	public async Task<IActionResult> UpdateAsync(
		[FromRoute] Guid assignmentId,
		[FromQuery] Guid academicMediumId,
		[FromBody] UpdateSubjectAssignmentGroupDto input
	)
	{
		if (academicMediumId == Guid.Empty)
		{
			return BadRequest(new
			{
				error = "Validation failed",
				details = new { academicMediumId = "academicMediumId is required" }
			});
		}

		var result = await subjectAssignmentAppService.UpdateAsync(
			assignmentId,
			academicMediumId,
			input
		);

		return Ok(result);
	}

	/// <summary>
	/// Publishes a draft assignment (changes status from Draft to Published)
	/// </summary>
	/// <param name="assignmentId">Assignment group ID</param>
	/// <returns>Publish result with new status and timestamp</returns>
	/// <response code="200">Assignment published successfully</response>
	/// <response code="404">Assignment not found</response>
	[HttpPatch("{assignmentId}/publish")]
	[ProducesResponseType(typeof(PublishResponseDto), 200)]
	[ProducesResponseType(404)]
	public async Task<IActionResult> PublishAsync([FromRoute] Guid assignmentId)
	{
		var result = await subjectAssignmentAppService.PublishAsync(assignmentId);
		return Ok(result);
	}

	/// <summary>
	/// Deletes a subject assignment group and all its subjects
	/// </summary>
	/// <param name="assignmentId">Assignment group ID</param>
	/// <returns>No content on success</returns>
	/// <response code="204">Assignment deleted successfully</response>
	/// <response code="404">Assignment not found</response>
	[HttpDelete("{assignmentId}")]
	[ProducesResponseType(204)]
	[ProducesResponseType(404)]
	public async Task<IActionResult> DeleteAsync([FromRoute] Guid assignmentId)
	{
		await subjectAssignmentAppService.DeleteAsync(assignmentId);
		return NoContent();
	}
}
