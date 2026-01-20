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

    [HttpDelete("{examId}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ScheduleExam.Delete)]
    public async Task DeleteAsync(Guid examId)
    {
        await examAppService.DeleteAsync(examId);
    }

    [HttpGet]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ScheduleExam.View)]
    public async Task<PagedResultDto<ExamTableItemDto>> GetListAsync(GetExamsInput input)
    {
        return await examAppService.GetListAsync(input);
    }
}
