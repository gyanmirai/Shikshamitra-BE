using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.Attendance;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Student.Attendance;

[Authorize, ApiController, Area("app"), RemoteService(Name = "Default")]
[Route("api/students/attendance")]
public class AttendanceManagementController(IAttendanceManagementAppService service) : AbpControllerBase
{
	[HttpGet("register")] public Task<AttendanceRegisterDto> Register([FromQuery] AttendanceRegisterInput input) => service.GetRegisterAsync(input);
	[HttpGet("summary")] public Task<AttendanceSummaryDto> Summary([FromQuery] AttendanceSummaryInput input) => service.GetSummaryAsync(input);
	[HttpGet("enrollments")] public Task<PagedResultDto<AttendanceEnrollmentDto>> Enrollments([FromQuery] AttendanceRegisterInput input) => service.GetEnrollmentsAsync(input);
	[HttpPost("enrollments/{enrollmentId:guid}/start-date")] public Task<AttendanceEnrollmentDto> ConfirmStart(Guid enrollmentId, [FromBody] ConfirmAttendanceStartInput input) => service.ConfirmStartAsync(enrollmentId, input);
	[HttpGet("corrections")] public Task<PagedResultDto<AttendanceCorrectionDto>> Corrections([FromQuery] AttendanceCorrectionListInput input) => service.GetCorrectionsAsync(input);
	[HttpPost("corrections")] public Task<AttendanceCorrectionDto> RequestCorrection([FromBody] AttendanceCorrectionInput input) => service.RequestCorrectionAsync(input);
	[HttpPost("corrections/{id:guid}/approve")] public Task<AttendanceCorrectionDto> Approve(Guid id, [FromBody] AttendanceCorrectionDecisionInput input) => service.ApproveCorrectionAsync(id, input);
	[HttpPost("corrections/{id:guid}/reject")] public Task<AttendanceCorrectionDto> Reject(Guid id, [FromBody] AttendanceCorrectionDecisionInput input) => service.RejectCorrectionAsync(id, input);
}
