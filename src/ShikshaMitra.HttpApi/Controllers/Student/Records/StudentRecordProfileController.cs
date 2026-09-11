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
[Route("api/students/records/{studentId:guid}/profile")]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View)]
public class StudentRecordProfileController(IStudentRecordProfileAppService service) : AbpController
{
	[HttpGet]
	public async Task<Result<StudentRecordProfileDto>> GetAsync(Guid studentId) =>
		Result.Success(await service.GetAsync(studentId));
}
