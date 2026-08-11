using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ShikshaMitra.Academic.Timetable.Substitutions;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Controllers.Academic.Timetable;

[Route("api/academics/timetable/substitutions")]
[Authorize]
[ApiController]
public class SubstitutionController(ISubstitutionAppService substitutionAppService) : ShikshaMitraController
{
    [HttpGet]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public Task<PagedResultDto<SubstitutionDto>> GetListAsync([FromQuery] GetSubstitutionsInput input)
    {
        return substitutionAppService.GetListAsync(input);
    }

    [HttpGet("dashboard")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public Task<SubstitutionDashboardDto> GetDashboardAsync(
        [FromQuery] DateTimeOffset date,
        [FromQuery] Guid? academicYearId = null,
        [FromQuery] Guid? academicMediumId = null)
    {
        return substitutionAppService.GetDashboardAsync(date, academicYearId, academicMediumId);
    }

    [HttpGet("my-duties")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public Task<PagedResultDto<SubstitutionDto>> GetMyDutiesAsync([FromQuery] GetSubstitutionsInput input)
    {
        return substitutionAppService.GetMyDutiesAsync(input);
    }

    [HttpGet("create-form-metadata")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public Task<SubstitutionCreateFormMetadataDto> GetCreateFormMetadataAsync([FromQuery] SubstitutionCreateFormMetadataInput input)
    {
        return substitutionAppService.GetCreateFormMetadataAsync(input);
    }

    [HttpGet("lectures")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public Task<List<SubstitutionLectureLookupDto>> GetLecturesAsync([FromQuery] SubstitutionLectureLookupInput input)
    {
        return substitutionAppService.GetLecturesAsync(input);
    }

    [HttpGet("{id:guid}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public Task<SubstitutionDto> GetAsync([FromRoute] Guid id)
    {
        return substitutionAppService.GetAsync(id);
    }

    [HttpPost]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Add)]
    public Task<SubstitutionDto> CreateAsync([FromBody] CreateSubstitutionDto input)
    {
        return substitutionAppService.CreateAsync(input);
    }

    [HttpPut("{id:guid}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Add)]
    public Task<SubstitutionDto> UpdateAsync([FromRoute] Guid id, [FromBody] UpdateSubstitutionDto input)
    {
        return substitutionAppService.UpdateAsync(id, input);
    }

    [HttpPost("{id:guid}/assign")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Add)]
    public Task<SubstitutionDto> AssignAsync([FromRoute] Guid id, [FromBody] AssignSubstituteDto input)
    {
        return substitutionAppService.AssignAsync(id, input);
    }

    [HttpPost("{id:guid}/send-reminder")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Add)]
    public Task<SubstitutionDto> SendReminderAsync([FromRoute] Guid id)
    {
        return substitutionAppService.SendReminderAsync(id);
    }

    [HttpPost("{id:guid}/complete")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Add)]
    public Task<SubstitutionDto> CompleteAsync([FromRoute] Guid id)
    {
        return substitutionAppService.CompleteAsync(id);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Delete)]
    public Task<SubstitutionDto> CancelAsync([FromRoute] Guid id, [FromBody] SubstitutionActionDto input)
    {
        return substitutionAppService.CancelAsync(id, input);
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Delete)]
    public Task<SubstitutionDto> RejectAsync([FromRoute] Guid id, [FromBody] SubstitutionActionDto input)
    {
        return substitutionAppService.RejectAsync(id, input);
    }

    [HttpPost("{id:guid}/acknowledge")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Add)]
    public Task<SubstitutionDto> AcknowledgeAsync(
        [FromRoute] Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] SubstitutionActionDto? input)
    {
        return substitutionAppService.AcknowledgeAsync(id, input);
    }

    [HttpPost("{id:guid}/request-change")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Add)]
    public Task<SubstitutionDto> RequestChangeAsync([FromRoute] Guid id, [FromBody] SubstitutionActionDto input)
    {
        return substitutionAppService.RequestChangeAsync(id, input);
    }

    [HttpPost("{id:guid}/reject-duty")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Add)]
    public Task<SubstitutionDto> RejectDutyAsync([FromRoute] Guid id, [FromBody] SubstitutionActionDto input)
    {
        return substitutionAppService.RejectDutyAsync(id, input);
    }

    [HttpGet("suggested-teachers")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public Task<List<SuggestedTeacherDto>> GetSuggestedTeachersAsync([FromQuery] SuggestedTeacherInput input)
    {
        return substitutionAppService.GetSuggestedTeachersAsync(input);
    }

    [HttpGet("{id:guid}/suggested-teachers")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public Task<List<SuggestedTeacherDto>> GetSuggestedTeachersAsync([FromRoute] Guid id)
    {
        return substitutionAppService.GetSuggestedTeachersAsync(id);
    }

    [HttpGet("{id:guid}/audit-logs")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public Task<List<SubstitutionAuditLogDto>> GetAuditLogsAsync([FromRoute] Guid id)
    {
        return substitutionAppService.GetAuditLogsAsync(id);
    }

    [HttpGet("daily-sheet")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public Task<SubstitutionDailySheetDto> GetDailySheetAsync([FromQuery] SubstitutionDailySheetInput input)
    {
        return substitutionAppService.GetDailySheetAsync(input);
    }

    [HttpGet("daily-sheet/pdf")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public async Task<IActionResult> GetDailySheetPdfAsync([FromQuery] SubstitutionDailySheetInput input)
    {
        var bytes = await substitutionAppService.GetDailySheetPdfAsync(input);
        return File(bytes, "application/pdf", "daily-substitution-sheet.pdf");
    }

    [HttpGet("daily-sheet/excel")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public async Task<IActionResult> GetDailySheetExcelAsync([FromQuery] SubstitutionDailySheetInput input)
    {
        var bytes = await substitutionAppService.GetDailySheetExcelAsync(input);
        return File(bytes, "text/csv", "daily-substitution-sheet.csv");
    }

    [HttpGet("analytics")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public Task<SubstitutionAnalyticsDto> GetAnalyticsAsync([FromQuery] SubstitutionAnalyticsInput input)
    {
        return substitutionAppService.GetAnalyticsAsync(input);
    }

}
