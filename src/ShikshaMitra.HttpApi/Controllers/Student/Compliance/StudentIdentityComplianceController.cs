using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Compliance;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.Compliance;

[ApiController]
[Authorize]
[Route("api/student/{studentId:guid}/identity-compliance")]
public class StudentIdentityComplianceController(
	IStudentIdentityComplianceAppService complianceAppService) : AbpControllerBase
{
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
	public async Task<Result<StudentIdentityComplianceDto>> GetAsync(Guid studentId)
	{
		return Result.Success(await complianceAppService.GetAsync(studentId));
	}

	[HttpPut]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.IdentityCompliance.ManageSensitive)]
	public async Task<Result<StudentIdentityComplianceDto>> UpdateAsync(
		Guid studentId,
		[FromBody] UpdateStudentIdentityComplianceDto input)
	{
		return Result.Success(await complianceAppService.UpdateAsync(studentId, input));
	}

	[HttpPost("aadhaar-verification-status")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.IdentityCompliance.ManageSensitive)]
	public async Task<Result<StudentIdentityComplianceDto>> UpdateAadhaarVerificationStatusAsync(
		Guid studentId,
		[FromBody] UpdateAadhaarVerificationStatusDto input)
	{
		return Result.Success(await complianceAppService.UpdateAadhaarVerificationStatusAsync(studentId, input));
	}

	[HttpPost("apaar-consent")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.IdentityCompliance.ManageSensitive)]
	public async Task<Result<StudentIdentityComplianceDto>> UpdateApaarConsentAsync(
		Guid studentId,
		[FromBody] UpdateApaarConsentDto input)
	{
		return Result.Success(await complianceAppService.UpdateApaarConsentAsync(studentId, input));
	}
}
