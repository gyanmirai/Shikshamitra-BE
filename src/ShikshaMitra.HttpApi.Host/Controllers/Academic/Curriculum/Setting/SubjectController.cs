using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Curriculum.Setting;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.Curriculum.Setting;

[Route("api/academic/curriculum/setting")]
[Authorize]
public class SubjectController(ISubjectAppService subjectAppService) : AbpController
{
	[HttpGet("subjects/{academicMediumId}")]
	public async Task<IActionResult> GetAllSubjects(Guid academicMediumId)
	{
		var subjects = await subjectAppService.GetListAsync(academicMediumId);
		return Ok(subjects);
	}

	[HttpPost("subject")]
	public async Task<IActionResult> CreateSubject(
		[FromBody] CreateSubjectDto createSubjectDto
	)
	{
		var subject = await subjectAppService.CreateSubjectAsync(createSubjectDto);
		return Ok(subject);
	}

	[HttpPut("subject/{id}")]
	public async Task<IActionResult> UpdateSubject(
		Guid id,
		[FromBody] UpdateSubjectDto updateSubjectDto
	)
	{
		var subject = await subjectAppService.UpdateSubjectAsync(id, updateSubjectDto);
		return Ok(subject);
	}

	[HttpDelete("subject/{id}")]
    public async Task<IActionResult> DeleteSubject(Guid id)
    {
        await subjectAppService.DeleteSubjectAsync(id);
        return Ok(new
        {
            message = "Subject has been deleted successfully.",          
        });
    }
}
