using System;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams.Dtos;
using ShikshaMitra.Common;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Examination.Exams;

public interface IExamAppService : IApplicationService
{
    Task CreateBasicDetailsAsync(ExamBasicDetailsRequestDto input);

    Task UpdateBasicDetailsAsync(Guid examId, ExamBasicDetailsRequestDto input);

    Task<ExamBasicDetailsResponseDto> GetBasicDetailsAsync(Guid examId);

    Task SaveScheduleAsync(Guid examId, ExamScheduleRequestDto input);

    Task<ExamScheduleResponseDto> GetScheduleAsync(Guid examId);

    Task AssignRoomsAsync(Guid examId, ExamRoomAssignmentRequestDto input);

    Task<ExamRoomAssignmentResponseDto> GetRoomAssignmentsAsync(Guid examId);
}
