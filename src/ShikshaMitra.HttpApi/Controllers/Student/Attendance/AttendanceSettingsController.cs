using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.Attendance;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Student.Attendance;

[ApiController, Authorize, Area("app"), RemoteService(Name = "Default")]
[Route("api/students/attendance/settings")]
public class AttendanceSettingsController(IAttendanceSettingsAppService service) : AbpControllerBase
{
	[HttpGet] public Task<AttendanceSettingsDto> GetAsync([FromQuery] Guid? academicYearId) => service.GetAsync(academicYearId);
	[HttpPut("draft")] public Task<AttendanceSettingsDto> SaveAsync([FromBody] SaveAttendanceSettingsDto input) => service.SaveDraftAsync(input);
	[HttpPost("preview")] public Task<AttendanceSettingsPreviewDto> PreviewAsync([FromBody] SaveAttendanceSettingsDto input) => service.PreviewAsync(input);
	[HttpPost("publish")] public Task<AttendanceSettingsDto> PublishAsync([FromBody] PublishAttendanceSettingsDto input) => service.PublishAsync(input);
}
