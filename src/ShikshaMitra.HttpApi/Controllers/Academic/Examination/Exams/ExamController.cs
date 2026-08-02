using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Exams.Dtos;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Controllers.Academic.Examination.Exams;

[RemoteService(Name = "ShikshaMitra")]
[Route("api/academics/exams")]
[Authorize]
[ApiController]
public class ExamController(
    IExamAppService examAppService
) : ShikshaMitraController
{
    [HttpPost("basic-details")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ScheduleExam.Add)]
    public async Task<IActionResult> CreateBasicDetailsAsync(ExamBasicDetailsRequestDto input)
    {
        var id = await examAppService.CreateBasicDetailsAsync(input);
        return Ok(new { id });
    }

    [HttpPut("{examId}/basic-details")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ScheduleExam.Add)]
    public async Task UpdateBasicDetailsAsync(Guid examId, ExamBasicDetailsRequestDto input)
    {
        await examAppService.UpdateBasicDetailsAsync(examId, input);
    }

    [HttpGet("{examId}/basic-details")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ScheduleExam.View)]

    public async Task<ExamBasicDetailsResponseDto> GetBasicDetailsAsync(Guid examId)
    {
        return await examAppService.GetBasicDetailsAsync(examId);
    }

    [HttpPut("{examId}/schedule")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ScheduleExam.Add)]
    public async Task SaveScheduleAsync(Guid examId, ExamScheduleRequestDto input)
    {
        await examAppService.SaveScheduleAsync(examId, input);
    }

    [HttpGet("{examId}/schedule")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ScheduleExam.View)]
    public async Task<ExamScheduleResponseDto> GetScheduleAsync(Guid examId)
    {
        return await examAppService.GetScheduleAsync(examId);
    }

    [HttpPut("{examId}/rooms")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ScheduleExam.Add)]
    public async Task AssignRoomsAsync(Guid examId, ExamRoomAssignmentRequestDto input)
    {
        await examAppService.AssignRoomsAsync(examId, input);
    }

    [HttpGet("{examId}/rooms")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ScheduleExam.View)]
    public async Task<ExamRoomAssignmentResponseDto> GetRoomAssignmentsAsync(Guid examId)
    {
        return await examAppService.GetRoomAssignmentsAsync(examId);
    }

    [HttpGet("{examId}/subjects")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ScheduleExam.View)]
    public async Task<List<ExamSubjectResponseDto>> GetSubjectsAsync(Guid examId)
    {
        return await examAppService.GetSubjectsAsync(examId);
    }

    [HttpGet("question-paper-lookup")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.View)]
    public async Task<List<QuestionPaperExamLookupDto>> GetQuestionPaperLookupAsync([FromQuery] QuestionPaperExamLookupInput input)
    {
        return await examAppService.GetQuestionPaperLookupAsync(input);
    }

    [HttpDelete("{examId}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ScheduleExam.Delete)]
    public async Task DeleteAsync(Guid examId)
    {
        await examAppService.DeleteAsync(examId);
    }

    [HttpGet]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ScheduleExam.View)]
    public async Task<ExamListResultDto> GetListAsync([FromQuery] GetExamsInput input)
    {
        return await examAppService.GetListAsync(input);
    }

    [HttpPut("{examId}/status")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ScheduleExam.Add)]
    public async Task ChangeStatusAsync(Guid examId, [FromBody] ChangeExamStatusDto input)
    {
        await examAppService.ChangeStatusAsync(examId, input.Status);
    }

    [HttpGet("marks")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.View)]
    public async Task<GetExamMarksResponseDto> GetMarksAsync([FromQuery] GetExamMarksInput input)
    {
        return await examAppService.GetMarksAsync(input);
    }

    [HttpPatch("{examId}/students/{studentId}/marks")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.Add)]
    public async Task<UpdateStudentMarksResponseDto> UpdateStudentMarksAsync(Guid examId, Guid studentId, [FromQuery] Guid subjectId, [FromBody] UpdateStudentMarksDto input)
    {
        return await examAppService.UpdateStudentMarksAsync(examId, studentId, subjectId, input);
    }

    [HttpPatch("{examId}/publish-marks")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.Add)]
    public async Task<PublishMarksResponseDto> PublishMarksAsync(Guid examId, [FromQuery] Guid divisionId)
    {
        return await examAppService.PublishMarksAsync(examId, divisionId);
    }

    [HttpPost("{examId}/marks/draft")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.Add)]
    public async Task<MarksWorkflowActionResponseDto> SaveDraftAsync(Guid examId, [FromQuery] MarksWorkflowActionInput input)
    {
        return await examAppService.SaveDraftAsync(examId, input);
    }

    [HttpPost("{examId}/marks/submit")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.Add)]
    public async Task<MarksWorkflowActionResponseDto> SubmitMarksAsync(Guid examId, [FromQuery] MarksWorkflowActionInput input)
    {
        return await examAppService.SubmitMarksAsync(examId, input);
    }

    [HttpPost("{examId}/marks/verify")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.Add)]
    public async Task<MarksWorkflowActionResponseDto> VerifyMarksAsync(Guid examId, [FromQuery] MarksWorkflowActionInput input)
    {
        return await examAppService.VerifyMarksAsync(examId, input);
    }

    [HttpPost("{examId}/marks/publish")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.Add)]
    public async Task<MarksWorkflowActionResponseDto> PublishAndLockAsync(Guid examId, [FromQuery] MarksWorkflowActionInput input)
    {
        return await examAppService.PublishAndLockAsync(examId, input);
    }

    [HttpPatch("{examId}/students/{studentId}/attendance")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.Add)]
    public async Task<UpdateStudentAttendanceResponseDto> UpdateStudentAttendanceAsync(
        Guid examId, Guid studentId, [FromQuery] Guid subjectId, [FromBody] UpdateStudentAttendanceDto input)
    {
        return await examAppService.UpdateStudentAttendanceAsync(examId, studentId, subjectId, input);
    }

    [HttpGet("marks/summary")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.View)]
    public async Task<GetExamMarksSummaryResponseDto> GetMarksSummaryAsync([FromQuery] GetExamMarksSummaryInput input)
    {
        return await examAppService.GetMarksSummaryAsync(input);
    }

    [HttpPost("{examId}/marks/bulk-save")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.Add)]
    public async Task<MarksWorkflowActionResponseDto> BulkSaveMarksAsync(Guid examId, [FromBody] BulkSaveMarksInputDto input)
    {
        return await examAppService.BulkSaveMarksAsync(examId, input);
    }

    [HttpPost("{examId}/marks/validate")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.View)]
    public async Task<MarksValidationResponseDto> ValidateMarksAsync(Guid examId, [FromQuery] MarksWorkflowActionInput input)
    {
        return await examAppService.ValidateMarksAsync(examId, input);
    }

    [HttpPost("{examId}/marks/request-changes")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.Add)]
    public async Task<MarksWorkflowActionResponseDto> RequestChangesAsync(Guid examId, [FromBody] RequestChangesInputDto input)
    {
        return await examAppService.RequestChangesAsync(examId, input);
    }

    [HttpPost("{examId}/marks/unlock")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.Add)]
    public async Task<MarksWorkflowActionResponseDto> UnlockMarksAsync(Guid examId, [FromBody] UnlockWorkflowInputDto input)
    {
        return await examAppService.UnlockMarksAsync(examId, input);
    }

    [HttpGet("{examId}/marks/audit")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.View)]
    public async Task<List<GradeEntryAuditLogDto>> GetAuditLogsAsync(Guid examId, [FromQuery] Guid subjectId, [FromQuery] Guid divisionId)
    {
        return await examAppService.GetAuditLogsAsync(examId, subjectId, divisionId);
    }
}
