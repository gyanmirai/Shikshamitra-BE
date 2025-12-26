using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.Curriculum;

/// <summary>
/// Controller for managing subjects within education mediums
/// </summary>
[Route("api/academic-mediums/{academicMediumId}/subjects")]
[Authorize]
public class SubjectController(ISubjectAppService subjectAppService) : AbpController
{
	/// <summary>
	/// Get all subjects for a specific education medium
	/// </summary>
	/// <param name="academicMediumId">Education Medium ID</param>
	/// <param name="searchTerm">Optional search term to filter subjects</param>
	/// <returns>List of subjects</returns>
	[HttpGet]
	[Authorize(
		ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_SettingsSubjectCreation
			.View
	)]
	public async Task<IActionResult> GetAllSubjects(
		Guid academicMediumId,
		[FromQuery] string? searchTerm = null
	)
	{
		var subjects = await subjectAppService.GetListAsync(academicMediumId, searchTerm);
		return Ok(subjects);
	}

	/// <summary>
	/// Get a single subject by ID
	/// </summary>
	/// <param name="academicMediumId">Education Medium ID</param>
	/// <param name="id">Subject ID</param>
	/// <returns>Subject details</returns>
	[HttpGet("{id}")]
	[Authorize(
		ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_SettingsSubjectCreation
			.View
	)]
	public async Task<IActionResult> GetSubject(Guid academicMediumId, Guid id)
	{
		var subject = await subjectAppService.GetAsync(id);

		// Verify the subject belongs to the specified education medium
		if (subject.EducationMediumId != academicMediumId)
		{
			return NotFound();
		}

		return Ok(subject);
	}

	/// <summary>
	/// Create a new subject
	/// </summary>
	/// <param name="academicMediumId">Education Medium ID</param>
	/// <param name="createSubjectDto">Subject creation data</param>
	/// <returns>Created subject details</returns>
	[HttpPost]
	[Authorize(
		ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_SettingsSubjectCreation
			.Add
	)]
	public async Task<IActionResult> CreateSubject(
		Guid academicMediumId,
		[FromBody] CreateUpdateSubjectDto createSubjectDto
	)
	{
		// Ensure the EducationMediumId in DTO matches the route parameter
		if (createSubjectDto.EducationMediumId != academicMediumId)
		{
			return BadRequest(
				"Education Medium ID in request body must match the route parameter"
			);
		}

		var subject = await subjectAppService.CreateAsync(createSubjectDto);
		return CreatedAtAction(
			nameof(GetSubject),
			new { academicMediumId, id = subject.Id },
			subject
		);
	}

	/// <summary>
	/// Update an existing subject
	/// </summary>
	/// <param name="academicMediumId">Education Medium ID</param>
	/// <param name="id">Subject ID</param>
	/// <param name="updateSubjectDto">Subject update data</param>
	/// <returns>Updated subject details</returns>
	[HttpPut("{id}")]
	[Authorize(
		ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_SettingsSubjectCreation
			.Add
	)]
	public async Task<IActionResult> UpdateSubject(
		Guid academicMediumId,
		Guid id,
		[FromBody] CreateUpdateSubjectDto updateSubjectDto
	)
	{
		// Ensure the EducationMediumId in DTO matches the route parameter
		if (updateSubjectDto.EducationMediumId != academicMediumId)
		{
			return BadRequest(
				"Education Medium ID in request body must match the route parameter"
			);
		}

		// Verify the existing subject belongs to the specified education medium before update
		var existingSubject = await subjectAppService.GetAsync(id);
		if (existingSubject.EducationMediumId != academicMediumId)
		{
			return NotFound();
		}

		var subject = await subjectAppService.UpdateAsync(id, updateSubjectDto);
		return Ok(subject);
	}

	/// <summary>
	/// Delete a subject
	/// </summary>
	/// <param name="academicMediumId">Education Medium ID</param>
	/// <param name="id">Subject ID</param>
	/// <returns>No content on success</returns>
	[HttpDelete("{id}")]
	[Authorize(
		ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_SettingsSubjectCreation
			.Delete
	)]
	public async Task<IActionResult> DeleteSubject(Guid academicMediumId, Guid id)
	{
		// Verify the subject belongs to the specified education medium before deletion
		var subject = await subjectAppService.GetAsync(id);
		if (subject.EducationMediumId != academicMediumId)
		{
			return NotFound();
		}

		await subjectAppService.DeleteAsync(id);
		return NoContent();
	}
}
