using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Records;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Students.Records;

[ApiController]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_AppAccess.View)]
[Route("api/students/records/{studentId:guid}/app-access")]
public class StudentRecordAppAccessController(IStudentRecordAppAccessAppService service) : AbpController
{
	[HttpGet]
	public async Task<Result<StudentRecordAppAccessDto>> GetAsync(Guid studentId) => Result.Success(await service.GetAsync(studentId));

	[HttpPut("{recipientKey}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_AppAccess.Add)]
	public async Task<Result<StudentRecordAppAccessDto>> UpdateAsync(Guid studentId, string recipientKey, [FromBody] UpdateStudentRecordAppAccessDto input) =>
		Result.Success(await service.UpdateAsync(studentId, recipientKey, input));
}
