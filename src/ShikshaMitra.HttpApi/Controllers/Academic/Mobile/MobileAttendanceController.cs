using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Mobile;
using ShikshaMitra.Common.Responses;
using Volo.Abp.Content;

namespace ShikshaMitra.Controllers.Academic.Mobile;

[ApiController, Authorize]
[Route("api/mobile/v1/academics/attendance")]
public class MobileAttendanceController(IMobileAttendanceAppService service) : ShikshaMitraController
{
	[HttpGet("overview")] public async Task<Result<MobileAttendanceOverviewDto>> Overview([FromQuery] MobileAcademicInput input) => Result.Success(await service.GetOverviewAsync(input));
	[HttpGet("calendar")] public async Task<Result<List<MobileAttendanceDayDto>>> Calendar([FromQuery] MobileCalendarInput input) => Result.Success(await service.GetCalendarAsync(input));
	[HttpPut("days/{date}/absence-reason")] public async Task<Result<MobileAttendanceDayDto>> Reason(DateOnly date, [FromBody] MobileAbsenceInput input) => Result.Success(await service.SetAbsenceReasonAsync(date, input));
	[HttpGet("leave-types")] public async Task<Result<List<MobileLeaveTypeDto>>> Types([FromQuery] MobileAcademicInput input) => Result.Success(await service.GetLeaveTypesAsync(input));
	[HttpGet("leave-requests")] public async Task<Result<MobilePage<MobileLeaveDto>>> Leaves([FromQuery] MobileListInput input) => Result.Success(await service.GetLeavesAsync(input));
	[HttpGet("leave-requests/{id:guid}")] public async Task<Result<MobileLeaveDto>> Leave(Guid id, [FromQuery] MobileAcademicInput input) => Result.Success(await service.GetLeaveAsync(id, input));
	[HttpPost("leave-requests")] public async Task<Result<MobileLeaveDto>> Create([FromBody] MobileLeaveInput input) => Result.Success(await service.SaveLeaveAsync(null, input));
	[HttpPut("leave-requests/{id:guid}")] public async Task<Result<MobileLeaveDto>> Update(Guid id, [FromBody] MobileLeaveInput input) => Result.Success(await service.SaveLeaveAsync(id, input));
	[HttpPatch("leave-requests/{id:guid}/status")] public async Task<Result<MobileLeaveDto>> Cancel(Guid id, [FromBody] MobileAcademicInput input) => Result.Success(await service.CancelLeaveAsync(id, input));
	[HttpPost("leave-requests/attachments"), RequestSizeLimit(6 * 1024 * 1024)]
	public async Task<Result<MobileAttachmentDto>> Upload([FromQuery] MobileAcademicInput input, IFormFile file)
	{
		using var stream = new RemoteStreamContent(file.OpenReadStream(), file.FileName, file.ContentType);
		return Result.Success(await service.UploadAsync(input, stream));
	}
	[HttpDelete("leave-requests/attachments/{attachmentId:guid}")]
	public async Task<IActionResult> Delete(Guid attachmentId, [FromQuery] MobileAcademicInput input) { await service.DeleteAttachmentAsync(attachmentId, input); return NoContent(); }
	[HttpGet("leave-requests/{id:guid}/attachments/{attachmentId:guid}/download")]
	public async Task<IActionResult> Download(Guid id, Guid attachmentId, [FromQuery] MobileAcademicInput input)
	{
		var file = await service.DownloadAsync(id, attachmentId, input);
		return File(file.Content, file.ContentType, file.Name);
	}
}
