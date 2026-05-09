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

    Task DeleteAsync(Guid examId);

    Task<ExamListResultDto> GetListAsync(GetExamsInput input);

    Task ChangeStatusAsync(Guid examId, ExamStatus status);

    Task<GetExamMarksResponseDto> GetMarksAsync(GetExamMarksInput input);

    Task<UpdateStudentMarksResponseDto> UpdateStudentMarksAsync(Guid examId, Guid studentId, Guid subjectId, UpdateStudentMarksDto input);

    Task<PublishMarksResponseDto> PublishMarksAsync(Guid examId, Guid divisionId);
}
