using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Curriculum;
using System;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.Curriculum;

[Route("api/subject")]
[Authorize]
public class SubjectController(ISubjectAppService subjectAppService) : AbpController
{
	[HttpGet]
	[Route("/api/mediums/{mediumId}/subjects")]
	public async Task<IActionResult> GetAllSubjects(Guid mediumId)
	{
		var subjects = await subjectAppService.GetListAsync(mediumId);
		return Ok(subjects);
	}

	[HttpPost]
	public async Task<IActionResult> CreateSubject([FromBody] CreateUpdateSubjectDto createSubjectDto)
	{
		var subject = await subjectAppService.CreateAsync(createSubjectDto);
		return Ok(subject);
	}

	[HttpPut("{id}")]
	public async Task<IActionResult> UpdateSubject(Guid id, [FromBody] CreateUpdateSubjectDto updateSubjectDto)
	{
		var subject = await subjectAppService.UpdateAsync(id, updateSubjectDto);
		return Ok(subject);
	}

	[HttpDelete("{id}")]
	public async Task<IActionResult> DeleteSubject(Guid id)
	{
		await subjectAppService.DeleteAsync(id);
		return NoContent();
	}
}
