using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Curriculum.SubjectAssignment;
using ShikshaMitra.Academic.Curriculum;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Volo.Abp.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace ShikshaMitra.Controllers.Academic.Curriculum;

[Route("api/academic/curriculum/standard-subject-assignment")]
[Authorize]
public class StandardSubjectAssignmentController(IStandardSubjectAssignmentAppService assignmentAppService) : AbpController
{
	[HttpPost]
	public async Task<StandardSubjectAssignmentResponseDto> CreateAssignmentAsync([FromBody] CreateStandardSubjectAssignmentDto createAssignmentDto)
	{
		return await assignmentAppService.CreateAssignmentAsync(createAssignmentDto);
	}

	[HttpPut("{assignmentId}/status")]
	public async Task ChangeStatusAsync(Guid assignmentId, [FromBody] SubjectStatus status)
	{
		await assignmentAppService.ChangeStatusAsync(assignmentId, status);
	}

	[HttpGet("{standardId}")]
	public async Task<List<StandardSubjectAssignmentResponseDto>> GetListAsync(Guid standardId)
	{
		return await assignmentAppService.GetListAsync(standardId);
	}

	[HttpPut("{assignmentId}/teachers")]
	public async Task UpdateTeachersAsync(Guid assignmentId, [FromQuery] Guid primaryId, [FromBody] List<Guid> secondaryIds)
	{
		await assignmentAppService.UpdateTeachersAsync(assignmentId, primaryId, secondaryIds);
	}
}
