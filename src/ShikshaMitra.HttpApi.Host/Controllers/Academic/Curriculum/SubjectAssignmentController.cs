using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Common.Responses;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.Curriculum;

/// <summary>
/// Controller for Subject Assignment operations
/// Aligned with FE contract: /api/subject-assignments
/// </summary>
[Route("api/academics/curriculum")]
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
	[ProducesResponseType(typeof(SubjectAssignmentDto), 201)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	public async Task<Result<SubjectAssignmentDto>> CreateAsync(
		[FromQuery] Guid academicMediumId,
		[FromBody] CreateUpdateSubjectAssignmentGroupDto input
	)
	{
		if (academicMediumId == Guid.Empty)
		{
			return Result.Failure<SubjectAssignmentDto>(
				new Error("Validation failed", "academicMediumId is required")
			);
		}

		var result = await subjectAssignmentAppService.CreateAsync(academicMediumId, input);
		return Result.Success(result);
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
	[HttpGet("{academicMediumId}/subjects/{classId}")]
	[ProducesResponseType(typeof(List<SubjectAssignmentDto>), 200)]
	[ProducesResponseType(400)]
	public async Task<Result<List<SubjectAssignmentDto>>> GetListAsync(
		Guid academicMediumId,
		Guid classId
	)
	{
		if (academicMediumId == Guid.Empty || classId == Guid.Empty)
		{
			return Result.Failure<List<SubjectAssignmentDto>>(
				new Error(
					"Validation failed",
					"academicMediumId and classId are required"
				)
			);
		}

		var result = await subjectAssignmentAppService.GetListAsync(
			academicMediumId,
			classId
		);

		return Result.Success(result);
	}

	/// <summary>
	/// Gets a single subject assignment group by ID
	/// </summary>
	/// <param name="assignmentId">Assignment group ID</param>
	/// <returns>Assignment group details</returns>
	/// <response code="200">Returns the assignment group</response>
	/// <response code="404">Assignment not found</response>
	[HttpGet("{assignmentId}")]
	[ProducesResponseType(typeof(SubjectAssignmentDto), 200)]
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
	[ProducesResponseType(typeof(SubjectAssignmentDto), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(404)]
	public async Task<IActionResult> UpdateAsync(
		[FromRoute] Guid assignmentId,
		[FromQuery] Guid academicMediumId,
		[FromBody] CreateUpdateSubjectAssignmentGroupDto input
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
	[HttpPatch("publish")]
	[ProducesResponseType(typeof(PublishResponseDto), 200)]
	[ProducesResponseType(404)]
	public async Task<Result<PublishResponseDto>> PublishAsync([FromBody] List<Guid> assignmentIds)
	{
		var result = await subjectAssignmentAppService.PublishAsync(assignmentIds);
		return Result.Success(result);
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
