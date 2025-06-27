using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.SubjectAssignment;
using ShikshaMitra.Extensions;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.Curriculum;

[Route("api/academic/curriculum/standard-subject-assignment")]
[Authorize]
public class StandardSubjectAssignmentController(
	IStandardSubjectAssignmentAppService assignmentAppService
) : AbpController
{
	[HttpPost]
	public async Task<IActionResult> CreateAssignmentAsync(
		[FromBody] List<CreateStandardSubjectAssignmentDto> createAssignmentDto
	)
	{
		var subjects = new List<StandardSubjectAssignmentResponseDto>();

		foreach (var dto in createAssignmentDto)
		{
			var subject = await assignmentAppService.CreateAssignmentAsync(
				createAssignmentDto: dto
			);
			subjects.Add(subject);
		}

		return Ok(subjects);
	}

	[HttpPut("{assignmentId}/status")]
	public async Task ChangeStatusAsync(Guid assignmentId, [FromBody] SubjectStatus status)
	{
		await assignmentAppService.ChangeStatusAsync(assignmentId, status);
	}

	[HttpGet("{standardId}")]
	public async Task<IActionResult> GetListAsync(Guid standardId)
	{
		var subjects = await assignmentAppService.GetListAsync(standardId);
		return Ok(subjects);
	}

	[HttpPut("{assignmentId}/teachers")]
	public async Task UpdateTeachersAsync(
		Guid assignmentId,
		[FromQuery] Guid primaryId,
		[FromBody] List<Guid> secondaryIds
	)
	{
		await assignmentAppService.UpdateTeachersAsync(assignmentId, primaryId, secondaryIds);
	}
}
