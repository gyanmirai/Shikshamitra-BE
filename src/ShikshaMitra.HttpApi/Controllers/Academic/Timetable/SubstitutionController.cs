using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Timetable.Substitutions;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Controllers.Academic.Timetable;

[Route("api/academics/timetable/substitutions")]
[Authorize]
[ApiController]
public class SubstitutionController(ISubstitutionAppService substitutionAppService) : ShikshaMitraController
{
    [HttpGet]
    public Task<PagedResultDto<SubstitutionDto>> GetListAsync([FromQuery] GetSubstitutionsInput input)
    {
        return substitutionAppService.GetListAsync(input);
    }

    [HttpGet("dashboard")]
    public Task<SubstitutionDashboardDto> GetDashboardAsync(
        [FromQuery] DateTime date,
        [FromQuery] Guid? academicYearId = null)
    {
        return substitutionAppService.GetDashboardAsync(date, academicYearId);
    }

    [HttpGet("my-duties")]
    public Task<PagedResultDto<SubstitutionDto>> GetMyDutiesAsync([FromQuery] GetSubstitutionsInput input)
    {
        return substitutionAppService.GetMyDutiesAsync(input);
    }

    [HttpGet("create-form-metadata")]
    public Task<SubstitutionCreateFormMetadataDto> GetCreateFormMetadataAsync()
    {
        return substitutionAppService.GetCreateFormMetadataAsync();
    }

    [HttpGet("lectures")]
    public Task<List<SubstitutionLectureLookupDto>> GetLecturesAsync([FromQuery] SubstitutionLectureLookupInput input)
    {
        return substitutionAppService.GetLecturesAsync(input);
    }

    [HttpGet("{id:guid}")]
    public Task<SubstitutionDto> GetAsync([FromRoute] Guid id)
    {
        return substitutionAppService.GetAsync(id);
    }

    [HttpPost]
    public Task<SubstitutionDto> CreateAsync([FromBody] CreateSubstitutionDto input)
    {
        return substitutionAppService.CreateAsync(input);
    }

    [HttpPut("{id:guid}")]
    public Task<SubstitutionDto> UpdateAsync([FromRoute] Guid id, [FromBody] UpdateSubstitutionDto input)
    {
        return substitutionAppService.UpdateAsync(id, input);
    }

    [HttpPost("{id:guid}/assign")]
    public Task<SubstitutionDto> AssignAsync([FromRoute] Guid id, [FromBody] AssignSubstituteDto input)
    {
        return substitutionAppService.AssignAsync(id, input);
    }

    [HttpPost("{id:guid}/send-reminder")]
    public Task<SubstitutionDto> SendReminderAsync([FromRoute] Guid id)
    {
        return substitutionAppService.SendReminderAsync(id);
    }

    [HttpPost("{id:guid}/complete")]
    public Task<SubstitutionDto> CompleteAsync([FromRoute] Guid id)
    {
        return substitutionAppService.CompleteAsync(id);
    }

    [HttpPost("{id:guid}/cancel")]
    public Task<SubstitutionDto> CancelAsync([FromRoute] Guid id, [FromBody] SubstitutionActionDto input)
    {
        return substitutionAppService.CancelAsync(id, input);
    }

    [HttpPost("{id:guid}/reject")]
    public Task<SubstitutionDto> RejectAsync([FromRoute] Guid id, [FromBody] SubstitutionActionDto input)
    {
        return substitutionAppService.RejectAsync(id, input);
    }

    [HttpPost("{id:guid}/acknowledge")]
    public Task<SubstitutionDto> AcknowledgeAsync([FromRoute] Guid id)
    {
        return substitutionAppService.AcknowledgeAsync(id);
    }

    [HttpPost("{id:guid}/request-change")]
    public Task<SubstitutionDto> RequestChangeAsync([FromRoute] Guid id, [FromBody] SubstitutionActionDto input)
    {
        return substitutionAppService.RequestChangeAsync(id, input);
    }

    [HttpPost("{id:guid}/reject-duty")]
    public Task<SubstitutionDto> RejectDutyAsync([FromRoute] Guid id, [FromBody] SubstitutionActionDto input)
    {
        return substitutionAppService.RejectDutyAsync(id, input);
    }

    [HttpGet("suggested-teachers")]
    public Task<List<SuggestedTeacherDto>> GetSuggestedTeachersAsync([FromQuery] SuggestedTeacherInput input)
    {
        return substitutionAppService.GetSuggestedTeachersAsync(input);
    }

    [HttpGet("{id:guid}/suggested-teachers")]
    public Task<List<SuggestedTeacherDto>> GetSuggestedTeachersAsync([FromRoute] Guid id)
    {
        return substitutionAppService.GetSuggestedTeachersAsync(id);
    }

    [HttpGet("{id:guid}/audit-logs")]
    public Task<List<SubstitutionAuditLogDto>> GetAuditLogsAsync([FromRoute] Guid id)
    {
        return substitutionAppService.GetAuditLogsAsync(id);
    }

    [HttpGet("daily-sheet")]
    public Task<SubstitutionDailySheetDto> GetDailySheetAsync([FromQuery] SubstitutionDailySheetInput input)
    {
        return substitutionAppService.GetDailySheetAsync(input);
    }

    [HttpGet("daily-sheet/pdf")]
    public async Task<IActionResult> GetDailySheetPdfAsync([FromQuery] SubstitutionDailySheetInput input)
    {
        var bytes = await substitutionAppService.GetDailySheetPdfAsync(input);
        return File(bytes, "application/pdf", "daily-substitution-sheet.pdf");
    }

    [HttpGet("daily-sheet/excel")]
    public async Task<IActionResult> GetDailySheetExcelAsync([FromQuery] SubstitutionDailySheetInput input)
    {
        var bytes = await substitutionAppService.GetDailySheetExcelAsync(input);
        return File(bytes, "text/csv", "daily-substitution-sheet.csv");
    }

    [HttpGet("analytics")]
    public Task<SubstitutionAnalyticsDto> GetAnalyticsAsync([FromQuery] SubstitutionAnalyticsInput input)
    {
        return substitutionAppService.GetAnalyticsAsync(input);
    }

    [HttpGet("settings")]
    public Task<SubstitutionSettingsDto> GetSettingsAsync([FromQuery] Guid? academicYearId = null)
    {
        return substitutionAppService.GetSettingsAsync(academicYearId);
    }

    [HttpPut("settings")]
    public Task<SubstitutionSettingsDto> UpdateSettingsAsync([FromBody] SubstitutionSettingsDto input)
    {
        return substitutionAppService.UpdateSettingsAsync(input);
    }
}
