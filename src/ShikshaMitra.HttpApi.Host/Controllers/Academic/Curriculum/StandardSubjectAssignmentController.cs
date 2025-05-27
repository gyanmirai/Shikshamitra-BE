using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Curriculum.SubjectAssignment;
using ShikshaMitra.Academic.Curriculum;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Volo.Abp.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Extensions;

namespace ShikshaMitra.Controllers.Academic.Curriculum;

[Route("api/academic/curriculum/standard-subject-assignment")]
[Authorize]
public class StandardSubjectAssignmentController(IStandardSubjectAssignmentAppService assignmentAppService) : AbpController
{
	[HttpPost]
	public async Task<IActionResult> CreateAssignmentAsync([FromBody] CreateStandardSubjectAssignmentDto createAssignmentDto)
	{
		var headers = this.Headers();
		var subject = await assignmentAppService.CreateAssignmentAsync(createAssignmentDto, headers.EducationMediumId);
		return Ok(subject);
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
	public async Task UpdateTeachersAsync(Guid assignmentId, [FromQuery] Guid primaryId, [FromBody] List<Guid> secondaryIds)
	{
		await assignmentAppService.UpdateTeachersAsync(assignmentId, primaryId, secondaryIds);
	}
}
