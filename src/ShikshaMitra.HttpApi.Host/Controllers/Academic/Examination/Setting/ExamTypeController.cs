using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Curriculum.Setting;
using ShikshaMitra.Academic.Examination.Setting;
using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Controllers.Academic.Examination.Setting;
[Route("api/academic/examination/setting")]
[Authorize]
[ApiController]
public class ExamTypeController(IExamTypeAppService examTypeAppService) : ControllerBase
{
	[HttpGet("exam-types/{academicMediumId}")]
	public async Task<IActionResult> GetAllExamTypes(Guid academicMediumId)
	{
		var examtypes = await examTypeAppService.GetExamTypeListAsync(academicMediumId);
		return Ok(examtypes);
	}

	[HttpPost("exam-type")]
	public async Task<IActionResult> CreateExamType(
		[FromBody] CreateExamTypeDto createExamTypeDto
	)
	{
		var subject = await examTypeAppService.CreateExamTypeAsync(createExamTypeDto);
		return Ok(subject);
	}

	[HttpPut("exam-type/{id}")]
	public async Task<IActionResult> UpdateExamType(
		Guid id,
		[FromBody] UpdateExamTypeDto updateExamTypeDto
	)
	{
		var subject = await examTypeAppService.UpdateExamTypeAsync(id, updateExamTypeDto);
		return Ok(subject);
	}

	[HttpDelete("exam-type/{id}")]
    public async Task<IActionResult> DeleteExamType(Guid id)
    {
        await examTypeAppService.DeleteExamTypeAsync(id);
        return Ok(new
        {
            message = "ExamType has been deleted successfully.",          
        });
    }
}
