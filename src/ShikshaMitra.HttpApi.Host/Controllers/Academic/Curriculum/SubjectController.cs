using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Extensions;
using System;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.Curriculum;

[Route("api/subject")]
[Authorize]
public class SubjectController(ISubjectAppService subjectAppService) : AbpController
{
	[HttpGet]
	[Route("/api/academic-mediums/{academicMediumId}/subjects")]
	public async Task<IActionResult> GetAllSubjects(Guid academicMediumId)
	{
		var subjects = await subjectAppService.GetListAsync(academicMediumId);
		return Ok(subjects);
	}

	[HttpPost]
	public async Task<IActionResult> CreateSubject([FromBody] CreateUpdateSubjectDto createSubjectDto)
	{
		var headers = this.Headers();
		var subject = await subjectAppService.CreateAsync(createSubjectDto, headers.EducationMediumId);
		return Ok(subject);
	}

	[HttpPut("{id}")]
	public async Task<IActionResult> UpdateSubject(Guid id, [FromBody] CreateUpdateSubjectDto updateSubjectDto)
	{
		var headers = this.Headers();
		var subject = await subjectAppService.UpdateAsync(id, updateSubjectDto, headers.EducationMediumId);
		return Ok(subject);
	}

	[HttpDelete("{id}")]
	public async Task<IActionResult> DeleteSubject(Guid id)
	{
		await subjectAppService.DeleteAsync(id);
		return NoContent();
	}
}
