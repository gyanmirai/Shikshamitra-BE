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
[Route("api/students/records/birthdays")]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Birthdays.View)]
public class StudentBirthdaysController(IStudentBirthdayAppService service) : AbpController
{
	[HttpGet]
	public async Task<Result<StudentBirthdayPageDto>> GetAsync([FromQuery] StudentBirthdayQueryDto input) =>
		Result.Success(await service.GetListAsync(input));

	[HttpGet("options")]
	public async Task<Result<StudentDirectoryOptionsDto>> GetOptionsAsync([FromQuery] Guid academicYearId) =>
		Result.Success(await service.GetOptionsAsync(academicYearId));
}
