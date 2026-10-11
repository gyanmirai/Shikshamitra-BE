using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Records;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Students.Records;

[ApiController]
[Route("api/students/records/directory")]
[Authorize]
public class StudentDirectoryController(IStudentDirectoryAppService service) : AbpController
{
	[HttpGet]
	public async Task<Result<PagedResultDto<StudentDirectoryItemDto>>> GetAsync([FromQuery] StudentDirectoryRequest input) => Result.Success(await service.GetListAsync(input));
	[HttpGet("options")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View)]
	public async Task<Result<StudentDirectoryOptionsDto>> OptionsAsync([FromQuery] Guid? academicYearId) => Result.Success(await service.GetOptionsAsync(academicYearId));
	[HttpGet("legacy-candidates")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View)]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Overrides.Request)]
	public async Task<Result<PagedResultDto<StudentDirectoryItemDto>>> GetLegacyCandidatesAsync([FromQuery] StudentDirectoryRequest input) => Result.Success(await service.GetLegacyCandidatesAsync(input));
	[HttpGet("legacy-overrides")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View)]
	public async Task<Result<PagedResultDto<StudentLegacyOverrideDto>>> GetOverridesAsync([FromQuery] PagedResultRequestDto input) => Result.Success(await service.GetLegacyOverridesAsync(input));
	[HttpPost("{studentId:guid}/legacy-overrides")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View)]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Overrides.Request)]
	public async Task<Result<StudentLegacyOverrideDto>> RequestAsync(Guid studentId, [FromBody] RequestStudentLegacyOverrideDto input) => Result.Success(await service.RequestLegacyOverrideAsync(studentId, input));
	[HttpPost("legacy-overrides/{id:guid}/approve")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View)]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Overrides.Approve)]
	public async Task<Result<StudentLegacyOverrideDto>> ApproveAsync(Guid id, [FromBody] DecideStudentLegacyOverrideDto input) => Result.Success(await service.ApproveLegacyOverrideAsync(id, input));
	[HttpPost("legacy-overrides/{id:guid}/reject")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View)]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Overrides.Approve)]
	public async Task<Result<StudentLegacyOverrideDto>> RejectAsync(Guid id, [FromBody] DecideStudentLegacyOverrideDto input) => Result.Success(await service.RejectLegacyOverrideAsync(id, input));
}
