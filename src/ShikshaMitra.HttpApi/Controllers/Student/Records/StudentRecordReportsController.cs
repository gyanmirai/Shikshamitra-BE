using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Records;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Students.Records;

[ApiController]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Reports.View)]
[Route("api/students/records/reports")]
public class StudentRecordReportsController(IStudentRecordReportAppService reports) : AbpController
{
	[HttpGet("preview")] public Task<StudentRecordReportPreviewDto> Preview([FromQuery] StudentRecordReportInput input) => reports.PreviewAsync(input);
	[HttpGet] public Task<PagedResultDto<StudentRecordReportJobDto>> List([FromQuery] PagedResultRequestDto input) => reports.GetListAsync(input);
	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Reports.Add)]
	public Task<StudentRecordReportJobDto> Create(StudentRecordReportInput input) => reports.CreateAsync(input);
	[HttpPost("{id}/cancel")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Reports.Add)]
	public Task<StudentRecordReportJobDto> Cancel(Guid id) => reports.CancelAsync(id);
	[HttpGet("{id}/download")]
	public async Task<IActionResult> Download(Guid id)
	{
		var file = await reports.DownloadAsync(id); return File(file.Content, file.ContentType, file.FileName);
	}
}
