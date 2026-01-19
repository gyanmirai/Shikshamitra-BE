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
}
