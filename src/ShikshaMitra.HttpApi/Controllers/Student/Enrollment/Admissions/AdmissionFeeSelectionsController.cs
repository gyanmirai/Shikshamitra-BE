using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[ApiController, Authorize]
[Route("api/student/enrollment/admissions/fee-selections/applications/{applicationId:guid}")]
public class AdmissionFeeSelectionsController(IAdmissionFeeSelectionAppService service) : AbpController
{
	[HttpGet]
	public async Task<Result<AdmissionFeeSelectionDto>> GetAsync(Guid applicationId) => Result.Success(await service.GetAsync(applicationId));
	[HttpGet("candidates")]
	public async Task<Result<List<AdmissionFeeStructureCandidateDto>>> GetCandidatesAsync(Guid applicationId) => Result.Success(await service.GetCandidatesAsync(applicationId));
	[HttpPut, Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Edit)]
	public async Task<Result<AdmissionFeeSelectionDto>> SaveAsync(Guid applicationId, SaveAdmissionFeeSelectionDto input) => Result.Success(await service.SaveAsync(applicationId, input));
	[HttpPost("confirm-onboarding"), Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	public async Task<Result<AdmissionFeeSelectionDto>> ConfirmOnboardingAsync(Guid applicationId, ConfirmAdmissionFeeSelectionDto input) => Result.Success(await service.ConfirmOnboardingAsync(applicationId, input));
}
