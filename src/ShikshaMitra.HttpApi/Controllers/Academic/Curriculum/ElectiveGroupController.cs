using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.Curriculum;

/// <summary>
/// Controller for managing elective groups within education mediums
/// </summary>
[Route("api/academic-mediums/{academicMediumId}/elective-groups")]
[Authorize]
public class ElectiveGroupController(IElectiveGroupAppService electiveGroupAppService)
	: AbpController
{
	/// <summary>
	/// Get all elective groups for a specific education medium
	/// </summary>
	/// <param name="academicMediumId">Education Medium ID</param>
	/// <param name="searchTerm">Optional search term to filter elective groups by name</param>
	/// <returns>List of elective groups with their associated subjects</returns>
	/// <response code="200">Returns the list of elective groups</response>
	/// <response code="403">User lacks View permission</response>
	[HttpGet]
	[Authorize(
		ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_SettingsElectiveGroupCreation
			.View
	)]
	[ProducesResponseType(typeof(List<ElectiveGroupDto>), 200)]
	public async Task<IActionResult> GetAllElectiveGroups(
		Guid academicMediumId,
		[FromQuery] string? searchTerm = null,
		[FromQuery] bool includeArchived = false
	)
	{
		var electiveGroups = await electiveGroupAppService.GetListAsync(
			academicMediumId,
			searchTerm,
			includeArchived
		);
		return Ok(electiveGroups);
	}

	/// <summary>
	/// Get a single elective group by ID
	/// </summary>
	/// <param name="academicMediumId">Education Medium ID</param>
	/// <param name="id">Elective Group ID</param>
	/// <returns>Elective group details with subjects</returns>
	/// <response code="200">Returns the elective group</response>
	/// <response code="404">Elective group not found or doesn't belong to specified medium</response>
	/// <response code="403">User lacks View permission</response>
	[HttpGet("{id}")]
	[Authorize(
		ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_SettingsElectiveGroupCreation
			.View
	)]
	[ProducesResponseType(typeof(ElectiveGroupDto), 200)]
	[ProducesResponseType(404)]
	public async Task<IActionResult> GetElectiveGroup(Guid academicMediumId, Guid id)
	{
		var electiveGroup = await electiveGroupAppService.GetAsync(academicMediumId, id);
		return Ok(electiveGroup);
	}

	/// <summary>
	/// Create a new elective group with selected subjects
	/// </summary>
	/// <param name="academicMediumId">Education Medium ID</param>
	/// <param name="createElectiveGroupDto">Elective group creation data</param>
	/// <returns>Created elective group details</returns>
	/// <response code="201">Elective group created successfully</response>
	/// <response code="400">Invalid input data or business rule violation</response>
	/// <response code="403">User lacks Add permission</response>
	/// <remarks>
	/// Sample request:
	///
	///     POST /api/academic-mediums/{academicMediumId}/elective-groups
	///     {
	///         "name": "Tech & Fitness Group",
	///         "color": "#FF5722",
	///         "subjectIds": [
	///             "4fa85f64-5717-4562-b3fc-2c963f66afa7",
	///             "5fa85f64-5717-4562-b3fc-2c963f66afa8"
	///         ]
	///     }
	///
	/// Business Rules:
	/// - Name must be unique within the academic medium
	/// - At least 2 subjects required
	/// - All subjects must exist and belong to the same academic medium
	/// </remarks>
	[HttpPost]
	[Authorize(
		ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_SettingsElectiveGroupCreation
			.Add
	)]
	[ProducesResponseType(typeof(ElectiveGroupDto), 201)]
	[ProducesResponseType(400)]
	public async Task<IActionResult> CreateElectiveGroup(
		Guid academicMediumId,
		[FromBody] CreateUpdateElectiveGroupDto createElectiveGroupDto
	)
	{
		var electiveGroup = await electiveGroupAppService.CreateAsync(
			academicMediumId,
			createElectiveGroupDto
		);

		return CreatedAtAction(
			nameof(GetElectiveGroup),
			new { academicMediumId, id = electiveGroup.Id },
			electiveGroup
		);
	}

	/// <summary>
	/// Update an existing elective group
	/// </summary>
	/// <param name="academicMediumId">Education Medium ID</param>
	/// <param name="id">Elective Group ID</param>
	/// <param name="updateElectiveGroupDto">Updated elective group data</param>
	/// <returns>Updated elective group details</returns>
	/// <response code="200">Elective group updated successfully</response>
	/// <response code="400">Invalid input data or business rule violation</response>
	/// <response code="404">Elective group not found or doesn't belong to specified medium</response>
	/// <response code="403">User lacks Add permission</response>
	[HttpPut("{id}")]
	[Authorize(
		ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_SettingsElectiveGroupCreation
			.Add
	)]
	[ProducesResponseType(typeof(ElectiveGroupDto), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(404)]
	public async Task<IActionResult> UpdateElectiveGroup(
		Guid academicMediumId,
		Guid id,
		[FromBody] CreateUpdateElectiveGroupDto updateElectiveGroupDto
	)
	{
		var electiveGroup = await electiveGroupAppService.UpdateAsync(
			academicMediumId,
			id,
			updateElectiveGroupDto
		);
		return Ok(electiveGroup);
	}

	[HttpPost("check-duplicate")]
	[Authorize(
		ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_SettingsElectiveGroupCreation
			.View
	)]
	public async Task<IActionResult> CheckDuplicate(
		Guid academicMediumId,
		[FromBody] ElectiveGroupDuplicateCheckDto input
	)
	{
		return Ok(await electiveGroupAppService.CheckDuplicateAsync(academicMediumId, input));
	}

	[HttpGet("{id}/usage")]
	[Authorize(
		ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_SettingsElectiveGroupCreation
			.View
	)]
	public async Task<IActionResult> GetUsage(Guid academicMediumId, Guid id)
	{
		return Ok(await electiveGroupAppService.GetUsageDetailsAsync(academicMediumId, id));
	}

	[HttpPost("{id}/archive")]
	[Authorize(
		ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_SettingsElectiveGroupCreation
			.Add
	)]
	public async Task<IActionResult> Archive(
		Guid academicMediumId,
		Guid id,
		[FromBody] ElectiveGroupStatusChangeDto input
	)
	{
		return Ok(await electiveGroupAppService.ArchiveAsync(academicMediumId, id, input));
	}

	[HttpPost("{id}/restore")]
	[Authorize(
		ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_SettingsElectiveGroupCreation
			.Add
	)]
	public async Task<IActionResult> Restore(Guid academicMediumId, Guid id)
	{
		return Ok(await electiveGroupAppService.RestoreAsync(academicMediumId, id));
	}

	[HttpGet("{id}/audit-logs")]
	[Authorize(
		ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_SettingsElectiveGroupCreation
			.View
	)]
	public async Task<IActionResult> GetAuditLogs(Guid academicMediumId, Guid id)
	{
		return Ok(await electiveGroupAppService.GetAuditLogsAsync(academicMediumId, id));
	}

	/// <summary>
	/// Delete an elective group
	/// </summary>
	/// <param name="academicMediumId">Education Medium ID</param>
	/// <param name="id">Elective Group ID</param>
	/// <returns>No content</returns>
	/// <response code="204">Elective group deleted successfully</response>
	/// <response code="404">Elective group not found or doesn't belong to specified medium</response>
	/// <response code="403">User lacks Delete permission</response>
	[HttpDelete("{id}")]
	[Authorize(
		ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_SettingsElectiveGroupCreation
			.Delete
	)]
	[ProducesResponseType(204)]
	[ProducesResponseType(404)]
	public async Task<IActionResult> DeleteElectiveGroup(Guid academicMediumId, Guid id)
	{
		await electiveGroupAppService.DeleteAsync(academicMediumId, id);
		return NoContent();
	}
}
