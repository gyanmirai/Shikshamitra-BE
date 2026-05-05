using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.HomeworkAssignment;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.HomeworkAssignment;

[ApiController]
[Authorize]
[Route("api/academics/homeworks/{homeworkId}/assessments")]
public class HomeworkAssessmentController(
    IStudentSubmissionAppService service
) : AbpControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(AssessmentListDto<HomeworkAssessmentItemDto>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetAssessments(
        Guid homeworkId,
        [FromQuery] GetAssessmentsInput input)
    {
        var result = await service.GetHomeworkAssessmentsAsync(homeworkId, input);
        return Ok(result);
    }
}
