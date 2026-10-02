using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.Attendance;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Student.Attendance;

[ApiController, Authorize, Area("app"), RemoteService(Name = "Default")]
[Route("api/students/attendance/schedules")]
public class AttendanceScheduleController(IAttendanceScheduleAppService service) : AbpControllerBase
{
	[HttpGet] public Task<ListResultDto<AttendanceScheduleDto>> GetAsync([FromQuery] Guid enrollmentId) => service.GetListAsync(enrollmentId);
	[HttpPost] public Task<AttendanceScheduleDto> SaveAsync([FromBody] SaveAttendanceScheduleDto input) => service.SaveAsync(input);
	[HttpDelete("{id:guid}")] public Task DeleteAsync(Guid id, [FromQuery] string concurrencyStamp) => service.DeleteAsync(id, concurrencyStamp);
}
