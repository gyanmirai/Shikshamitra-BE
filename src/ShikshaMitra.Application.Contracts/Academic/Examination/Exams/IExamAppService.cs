using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams.Dtos;
using ShikshaMitra.Common;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Examination.Exams;

public interface IExamAppService : IApplicationService
{
    Task<Guid> CreateBasicDetailsAsync(ExamBasicDetailsRequestDto input);

    Task UpdateBasicDetailsAsync(Guid examId, ExamBasicDetailsRequestDto input);

    Task<ExamBasicDetailsResponseDto> GetBasicDetailsAsync(Guid examId);

    Task SaveScheduleAsync(Guid examId, ExamScheduleRequestDto input);

    Task<ExamScheduleResponseDto> GetScheduleAsync(Guid examId);

    Task AssignRoomsAsync(Guid examId, ExamRoomAssignmentRequestDto input);

    Task<ExamRoomAssignmentResponseDto> GetRoomAssignmentsAsync(Guid examId);

    Task<List<ExamSubjectResponseDto>> GetSubjectsAsync(Guid examId);

    Task<List<QuestionPaperExamLookupDto>> GetQuestionPaperLookupAsync(QuestionPaperExamLookupInput input);

    Task DeleteAsync(Guid examId);

    Task<ExamListResultDto> GetListAsync(GetExamsInput input);

    Task ChangeStatusAsync(Guid examId, ExamStatus status);

    Task<GetExamMarksResponseDto> GetMarksAsync(GetExamMarksInput input);

    Task<UpdateStudentMarksResponseDto> UpdateStudentMarksAsync(Guid examId, Guid studentId, Guid subjectId, UpdateStudentMarksDto input);

    Task<PublishMarksResponseDto> PublishMarksAsync(Guid examId, Guid divisionId);

    // Workflow endpoints
    Task<MarksWorkflowActionResponseDto> SaveDraftAsync(Guid examId, MarksWorkflowActionInput input);

    Task<MarksWorkflowActionResponseDto> SubmitMarksAsync(Guid examId, MarksWorkflowActionInput input);

    Task<MarksWorkflowActionResponseDto> VerifyMarksAsync(Guid examId, MarksWorkflowActionInput input);

    Task<MarksWorkflowActionResponseDto> PublishAndLockAsync(Guid examId, MarksWorkflowActionInput input);

    Task<UpdateStudentAttendanceResponseDto> UpdateStudentAttendanceAsync(Guid examId, Guid studentId, Guid subjectId, UpdateStudentAttendanceDto input);

    Task<GetExamMarksSummaryResponseDto> GetMarksSummaryAsync(GetExamMarksSummaryInput input);

    Task<MarksWorkflowActionResponseDto> BulkSaveMarksAsync(Guid examId, BulkSaveMarksInputDto input);

    Task<MarksValidationResponseDto> ValidateMarksAsync(Guid examId, MarksWorkflowActionInput input);

    Task<MarksWorkflowActionResponseDto> RequestChangesAsync(Guid examId, RequestChangesInputDto input);

    Task<MarksWorkflowActionResponseDto> UnlockMarksAsync(Guid examId, UnlockWorkflowInputDto input);

    Task<List<GradeEntryAuditLogDto>> GetAuditLogsAsync(Guid examId, Guid subjectId, Guid divisionId);
}
