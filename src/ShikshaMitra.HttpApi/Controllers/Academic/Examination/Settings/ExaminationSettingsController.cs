using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Constants.Permissions;

namespace ShikshaMitra.Controllers.Academic.Examination;

[Route("api/academics/examination/settings")]
[Authorize]
[ApiController]
public class ExaminationSettingsController(
	IExaminationSettingsAppService examinationSettingsAppService
) : ShikshaMitraController
{
    [HttpPost("grading-scales")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SettingsGradeScaling.Add)]
    public async Task CreateGradingScaleAsync(
        [FromQuery] Guid academicMediumId,
        [FromBody] CreateGradingScaleDto input
    )
    {
        await examinationSettingsAppService.CreateGradingScaleAsync(academicMediumId, input);
    }

    [HttpGet("grading-scales")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SettingsGradeScaling.View)]
    public async Task<GradingScaleListResponseDto> GetAllGradingScalesAsync(
        [FromQuery] Guid academicMediumId
    )
    {
        return await examinationSettingsAppService.GetAllGradingScalesAsync(academicMediumId);
    }

    [HttpGet("grading-scales/{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SettingsGradeScaling.View)]
    public async Task<GradingScaleSingleResponseDto> GetGradingScaleByIdAsync(
        [FromRoute] Guid id,
        [FromQuery] Guid academicMediumId
    )
    {
        return await examinationSettingsAppService.GetGradingScaleByIdAsync(id, academicMediumId);
    }

    [HttpPut("grading-scales/{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SettingsGradeScaling.Add)]
    public async Task<IActionResult> UpdateGradingScaleAsync(
        [FromRoute] Guid id,
        [FromQuery] Guid academicMediumId,
        [FromBody] UpdateGradingScaleDto input
    )
    {
        await examinationSettingsAppService.UpdateGradingScaleAsync(id, academicMediumId, input);
        return Ok(new { message = "Grading scale updated successfully" });
    }

    [HttpDelete("grading-scales/{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SettingsGradeScaling.Delete)]
    public async Task<IActionResult> DeleteGradingScaleAsync(
        [FromRoute] Guid id,
        [FromQuery] Guid academicMediumId
    )
    {
        await examinationSettingsAppService.DeleteGradingScaleAsync(id, academicMediumId);
        return Ok(new { message = "Grading scale deleted successfully" });
    }
    [HttpPost("exam-type")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SettingsExamTypes.Add)]
    public async Task CreateExamTypeAsync(
        [FromQuery] Guid academicMediumId,
        [FromBody] CreateExamTypeDto input
    )
    {
        await examinationSettingsAppService.CreateExamTypeAsync(academicMediumId, input);
    }
}
