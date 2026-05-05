using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.HomeworkAssignment;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.HomeworkAssignment;

[ApiController]
[Authorize]
[Route("api/academics/assignments/{assignmentId}/assessments")]
public class AssignmentAssessmentController(
    IStudentSubmissionAppService service
) : AbpControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(AssessmentListDto<AssignmentAssessmentItemDto>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetAssessments(
        Guid assignmentId,
        [FromQuery] GetAssessmentsInput input)
    {
        var result = await service.GetAssignmentAssessmentsAsync(assignmentId, input);
        return Ok(result);
    }
}
