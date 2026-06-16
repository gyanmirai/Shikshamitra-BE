using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.Academic.Examination.Results.Dtos;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;

namespace ShikshaMitra.Controllers.Academic.Examination.Results;

[RemoteService(Name = "ShikshaMitra")]
[Route("api/academics/results")]
[Authorize]
[ApiController]
public class ExamResultController(
    IExamResultAppService examResultAppService
) : ShikshaMitraController
{
    /// <summary>
    /// Returns a readiness checklist for result generation.
    /// </summary>
    [HttpGet("readiness")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ResultCreation.View)]
    public async Task<ResultReadinessDto> GetReadinessAsync([FromQuery] Guid examId, [FromQuery] Guid divisionId)
    {
        return await examResultAppService.GetReadinessAsync(examId, divisionId);
    }

    /// <summary>
    /// Generates (or regenerates) the result for an Exam + Division.
    /// </summary>
    [HttpPost("generate")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ResultCreation.Generate)]
    public async Task<ResultHeaderDto> GenerateAsync([FromBody] GenerateResultRequestDto input)
    {
        return await examResultAppService.GenerateAsync(input);
    }

    /// <summary>
    /// Publishes the result (Generated → Published).
    /// </summary>
    [HttpPost("publish")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ResultCreation.Publish)]
    public async Task<ResultHeaderDto> PublishAsync([FromBody] PublishResultRequestDto input)
    {
        return await examResultAppService.PublishAsync(input);
    }

    /// <summary>
    /// Freezes the result (Published → Frozen).
    /// </summary>
    [HttpPost("freeze")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ResultCreation.Freeze)]
    public async Task<ResultHeaderDto> FreezeAsync([FromBody] FreezeResultRequestDto input)
    {
        return await examResultAppService.FreezeAsync(input);
    }

    /// <summary>
    /// Returns aggregate analytics for a result header.
    /// </summary>
    [HttpGet("analytics")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ResultCreation.View)]
    public async Task<ResultAnalyticsDto> GetAnalyticsAsync([FromQuery] Guid resultHeaderId)
    {
        return await examResultAppService.GetAnalyticsAsync(resultHeaderId);
    }

    /// <summary>
    /// Returns the current result header for a given Exam + Division, or 404 if not generated.
    /// </summary>
    [HttpGet]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ResultCreation.View)]
    public async Task<IActionResult> GetHeaderAsync([FromQuery] Guid examId, [FromQuery] Guid divisionId)
    {
        var result = await examResultAppService.GetHeaderAsync(examId, divisionId);
        if (result is null) return NotFound();
        return Ok(result);
    }
}
