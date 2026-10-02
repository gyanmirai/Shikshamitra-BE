using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Students.Records;

[ApiController]
[Area("app")]
[RemoteService(Name = "Default")]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Settings.View)]
[Route("api/students/records/settings")]
public class StudentRecordsSettingsController(IStudentRecordsSettingsAppService service) : AbpController
{
	[HttpGet("roles")]
	public async Task<Result<StudentRecordsRoleNamesDto>> GetRoleNamesAsync() => Result.Success(await service.GetRoleNamesAsync());
	[HttpGet] public async Task<Result<StudentRecordsSettingsDto>> GetAsync([FromQuery] Guid? academicYearId) => Result.Success(await service.GetAsync(academicYearId));
	[HttpPut("draft")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Settings.Add)]
	public async Task<Result<StudentRecordsSettingsDto>> SaveDraftAsync(SaveStudentRecordsSettingsDto input) => Result.Success(await service.SaveDraftAsync(input));
	[HttpPost("preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Settings.Add)]
	public async Task<Result<StudentRecordsSettingsPreviewDto>> PreviewAsync(SaveStudentRecordsSettingsDto input) => Result.Success(await service.PreviewAsync(input));
	[HttpPost("publish")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Settings.Publish)]
	public async Task<Result<StudentRecordsSettingsDto>> PublishAsync(PublishStudentRecordsSettingsDto input) => Result.Success(await service.PublishAsync(input));
}
