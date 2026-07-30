using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.HomeworkAssignment;

/// <summary>
/// Controller for managing assignment types per academic medium
/// </summary>
[Route("api/assignment-type")]
[ApiController]
[Authorize]
public class AssignmentTypeController : AbpController
{
	private readonly IAssignmentTypeAppService _service;

	public AssignmentTypeController(IAssignmentTypeAppService service)
	{
		_service = service;
	}

	/// <summary>
	/// Create a new assignment type
	/// </summary>
	/// <param name="academicMediumId">Academic medium ID (required query parameter)</param>
	/// <param name="input">Assignment type details</param>
	/// <returns>Created assignment type</returns>
	/// <response code="200">Assignment type created successfully</response>
	/// <response code="400">Validation failed</response>
	/// <response code="404">Academic medium not found</response>
	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsAssignmentTypes.Add)]
	[ProducesResponseType(typeof(AssignmentTypeCreateResponseDto), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(404)]
	public async Task<IActionResult> CreateAsync(
		[FromQuery, Required] Guid academicMediumId,
		[FromBody] CreateAssignmentTypeDto input
	)
	{
		if (academicMediumId == Guid.Empty)
		{
			return BadRequest(
				new
				{
					error = "Validation failed",
					details = new { academicMediumId = "academicMediumId is required" }
				}
			);
		}

		var result = await _service.CreateAsync(academicMediumId, input);
		return Ok(result);
	}

	/// <summary>
	/// Get all assignment types for an academic medium
	/// </summary>
	/// <param name="academicMediumId">Academic medium ID (required query parameter)</param>
	/// <returns>List of assignment types</returns>
	/// <response code="200">Success</response>
	/// <response code="404">Academic medium not found</response>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsAssignmentTypes.View)]
	[ProducesResponseType(typeof(AssignmentTypeListResponseDto), 200)]
	[ProducesResponseType(404)]
	public async Task<IActionResult> GetAllAsync(
		[FromQuery, Required] Guid academicMediumId,
		[FromQuery] bool includeArchived = false
	)
	{
		if (academicMediumId == Guid.Empty)
		{
			return BadRequest(
				new
				{
					error = "Validation failed",
					details = new { academicMediumId = "academicMediumId is required" }
				}
			);
		}

		var result = await _service.GetAllAsync(academicMediumId, includeArchived);
		return Ok(result);
	}

	/// <summary>
	/// Get assignment type by ID
	/// </summary>
	/// <param name="id">Assignment type ID</param>
	/// <param name="academicMediumId">Academic medium ID (required query parameter)</param>
	/// <returns>Assignment type details</returns>
	/// <response code="200">Success</response>
	/// <response code="404">Assignment type not found</response>
	[HttpGet("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsAssignmentTypes.View)]
	[ProducesResponseType(typeof(AssignmentTypeGetResponseDto), 200)]
	[ProducesResponseType(404)]
	public async Task<IActionResult> GetAsync(
		[FromRoute] Guid id,
		[FromQuery, Required] Guid academicMediumId
	)
	{
		if (academicMediumId == Guid.Empty)
		{
			return BadRequest(
				new
				{
					error = "Validation failed",
					details = new { academicMediumId = "academicMediumId is required" }
				}
			);
		}

		var result = await _service.GetAsync(id, academicMediumId);
		return Ok(result);
	}

	/// <summary>
	/// Update assignment type
	/// </summary>
	/// <param name="id">Assignment type ID</param>
	/// <param name="academicMediumId">Academic medium ID (required query parameter)</param>
	/// <param name="input">Updated assignment type details</param>
	/// <returns>Success message</returns>
	/// <response code="200">Assignment type updated successfully</response>
	/// <response code="400">Validation failed</response>
	/// <response code="404">Assignment type not found</response>
	[HttpPut("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsAssignmentTypes.Add)]
	[ProducesResponseType(typeof(MessageResponseDto), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(404)]
	public async Task<IActionResult> UpdateAsync(
		[FromRoute] Guid id,
		[FromQuery, Required] Guid academicMediumId,
		[FromBody] UpdateAssignmentTypeDto input
	)
	{
		if (academicMediumId == Guid.Empty)
		{
			return BadRequest(
				new
				{
					error = "Validation failed",
					details = new { academicMediumId = "academicMediumId is required" }
				}
			);
		}

		var result = await _service.UpdateAsync(id, academicMediumId, input);
		return Ok(result);
	}

	/// <summary>
	/// Delete assignment type
	/// </summary>
	/// <param name="id">Assignment type ID</param>
	/// <param name="academicMediumId">Academic medium ID (required query parameter)</param>
	/// <returns>Success message</returns>
	/// <response code="200">Assignment type deleted successfully</response>
	/// <response code="404">Assignment type not found</response>
	[HttpDelete("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsAssignmentTypes.Delete)]
	[ProducesResponseType(typeof(MessageResponseDto), 200)]
	[ProducesResponseType(404)]
	public async Task<IActionResult> DeleteAsync(
		[FromRoute] Guid id,
		[FromQuery, Required] Guid academicMediumId
	)
	{
		if (academicMediumId == Guid.Empty)
		{
			return BadRequest(
				new
				{
					error = "Validation failed",
					details = new { academicMediumId = "academicMediumId is required" }
				}
			);
		}

		var result = await _service.DeleteAsync(id, academicMediumId);
		return Ok(result);
	}

	[HttpPost("{id}/archive")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsAssignmentTypes.Delete)]
	[ProducesResponseType(typeof(MessageResponseDto), 200)]
	[ProducesResponseType(404)]
	public async Task<IActionResult> ArchiveAsync(
		[FromRoute] Guid id,
		[FromQuery, Required] Guid academicMediumId
	)
	{
		if (academicMediumId == Guid.Empty)
		{
			return BadRequest(new { error = "Validation failed", details = new { academicMediumId = "academicMediumId is required" } });
		}

		var result = await _service.ArchiveAsync(id, academicMediumId);
		return Ok(result);
	}

	[HttpPost("{id}/restore")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsAssignmentTypes.Add)]
	[ProducesResponseType(typeof(MessageResponseDto), 200)]
	[ProducesResponseType(404)]
	public async Task<IActionResult> RestoreAsync(
		[FromRoute] Guid id,
		[FromQuery, Required] Guid academicMediumId
	)
	{
		if (academicMediumId == Guid.Empty)
		{
			return BadRequest(new { error = "Validation failed", details = new { academicMediumId = "academicMediumId is required" } });
		}

		var result = await _service.RestoreAsync(id, academicMediumId);
		return Ok(result);
	}
}
