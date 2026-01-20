using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Exams.Dtos;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;

namespace ShikshaMitra.Controllers.Academic.Examination.Exams;

[RemoteService(Name = "ShikshaMitra")]
[Route("exams")]
public class ExamController(
	IExamAppService examAppService
) : ShikshaMitraController
{
    [HttpPost("basic-details")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ScheduleExam.Add)]
    public async Task CreateBasicDetailsAsync(ExamBasicDetailsRequestDto input)
    {
        await examAppService.CreateBasicDetailsAsync(input);
    }

    [HttpPut("{examId}/basic-details")]
    // [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ScheduleExam.Edit)] // Uncomment when permission is confirmed
    public async Task UpdateBasicDetailsAsync(Guid examId, ExamBasicDetailsRequestDto input)
    {
        await examAppService.UpdateBasicDetailsAsync(examId, input);
    }

    [HttpGet("{examId}/basic-details")]
    public async Task<ExamBasicDetailsResponseDto> GetBasicDetailsAsync(Guid examId)
    {
        return await examAppService.GetBasicDetailsAsync(examId);
    }

    [HttpPut("{examId}/schedule")]
    public async Task SaveScheduleAsync(Guid examId, ExamScheduleRequestDto input)
    {
        await examAppService.SaveScheduleAsync(examId, input);
    }

    [HttpGet("{examId}/schedule")]
    public async Task<ExamScheduleResponseDto> GetScheduleAsync(Guid examId)
    {
        return await examAppService.GetScheduleAsync(examId);
    }

    [HttpPut("{examId}/rooms")]
    public async Task AssignRoomsAsync(Guid examId, ExamRoomAssignmentRequestDto input)
    {
        await examAppService.AssignRoomsAsync(examId, input);
    }

    [HttpGet("{examId}/rooms")]
    public async Task<ExamRoomAssignmentResponseDto> GetRoomAssignmentsAsync(Guid examId)
    {
        return await examAppService.GetRoomAssignmentsAsync(examId);
    }
}
