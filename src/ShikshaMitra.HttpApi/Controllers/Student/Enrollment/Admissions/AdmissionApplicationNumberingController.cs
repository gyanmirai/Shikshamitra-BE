using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using Volo.Abp.AspNetCore.Mvc;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.Admissions;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[ApiController]
[Authorize]
[Route("api/student/enrollment/admissions/application-numbering")]
public class AdmissionApplicationNumberingController(IAdmissionApplicationNumberingAppService service) : AbpController
{
	[HttpGet]
	[Authorize(Permissions.View)]
	public async Task<Result<AdmissionApplicationNumberingDto>> Get() => Result.Success(await service.GetAsync());
	[HttpPut]
	[Authorize(Permissions.Add)]
	public async Task<Result<AdmissionApplicationNumberingDto>> Save([FromBody] SaveAdmissionApplicationNumberingDto input) => Result.Success(await service.SaveAsync(input));
	[HttpPost("preview")]
	[Authorize(Permissions.View)]
	public async Task<Result<AdmissionApplicationNumberPreviewDto>> Preview([FromBody] PreviewAdmissionApplicationNumberingDto input) => Result.Success(await service.PreviewAsync(input));
}
