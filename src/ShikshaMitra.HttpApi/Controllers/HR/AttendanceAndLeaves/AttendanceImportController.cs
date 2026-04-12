using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR;
using ShikshaMitra.HR.AttendanceAndLeaves.Import;
using ShikshaMitra.HttpApi.Models;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.AttendanceAndLeaves;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/attendance-import")]
[Authorize]
public class AttendanceImportController(IAttendanceImportAppService attendanceImportAppService)
	: AbpControllerBase
{
	[HttpPost("preview")]
	[Consumes("multipart/form-data")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_ImportAttendance.Add)]
	[ProducesResponseType(typeof(AttendanceImportPreviewResponseDto), StatusCodes.Status200OK)]
	public async Task<ActionResult<AttendanceImportPreviewResponseDto>> PreviewAsync([FromForm] AttendanceImportUploadRequest request)
	{
		if (request.File == null || request.File.Length == 0)
			throw new BusinessException(HRErrorCodes.AttendanceImportFileEmpty);

		await using var memoryStream = new MemoryStream();
		await request.File.CopyToAsync(memoryStream);

		var response = await attendanceImportAppService.PreviewAsync(
			request.File.FileName,
			request.File.ContentType,
			memoryStream.ToArray());

		return Ok(response);
	}

	[HttpPost("confirm")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_ImportAttendance.Add)]
	[ProducesResponseType(typeof(AttendanceImportConfirmResponseDto), StatusCodes.Status200OK)]
	public async Task<ActionResult<AttendanceImportConfirmResponseDto>> ConfirmAsync(
		[FromBody, Required] AttendanceImportConfirmRequestDto input)
	{
		var response = await attendanceImportAppService.ConfirmAsync(input);
		return Ok(response);
	}

	[HttpGet("template")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_ImportAttendance.View)]
	[ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
	public async Task<IActionResult> DownloadTemplateAsync()
	{
		var template = await attendanceImportAppService.GetTemplateAsync();
		return File(template.Content, template.ContentType, template.FileName);
	}
}



