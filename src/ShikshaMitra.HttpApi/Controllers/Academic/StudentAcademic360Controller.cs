using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.StudentAcademic360;
using ShikshaMitra.Constants.Permissions;

namespace ShikshaMitra.Controllers.Academic;

[Route("api/academics/student-academic-360")]
[Authorize]
[ApiController]
public class StudentAcademic360Controller(
    IStudentAcademic360AppService studentAcademic360AppService
) : ShikshaMitraController
{
    [HttpGet("{studentId:guid}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Dashboard.View)]
    public async Task<StudentAcademic360Dto> GetAsync(
        [FromRoute] Guid studentId,
        [FromQuery] StudentAcademic360Input input)
    {
        return await studentAcademic360AppService.GetAsync(studentId, input);
    }
}
