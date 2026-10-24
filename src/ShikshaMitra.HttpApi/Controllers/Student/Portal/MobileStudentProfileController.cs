using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Students.Portal;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Students.Portal;

[ApiController]
[Authorize]
[Route("api/mobile/v1/students/{studentId:guid}/profile")]
public class MobileStudentProfileController(IMobileStudentProfileAppService service) : AbpController
{
	[HttpGet]
	public async Task<Result<MobileStudentProfileDto>> GetAsync(Guid studentId, [FromQuery] Guid? academicYearId = null) =>
		Result.Success(await service.GetAsync(studentId, academicYearId));
}
