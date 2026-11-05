using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionApplications;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[ApiController, Area("app"), RemoteService(Name = "Default"), Authorize(Permissions.View)]
[Route("api/student/enrollment/admissions/applications/{applicationId:guid}/parent-registration")]
public class AdmissionParentRegistrationController(IAdmissionParentRegistrationAppService service) : AbpController
{
	[HttpGet] public async Task<Result<AdmissionParentStaffContextDto>> GetAsync(Guid applicationId) => Result.Success(await service.GetAsync(applicationId));
	[HttpPost("send"), Authorize(Permissions.Edit)] public async Task<Result<AdmissionParentStaffContextDto>> SendAsync(Guid applicationId, AdmissionParentSendInput input) => Result.Success(await service.SendAsync(applicationId, input));
	[HttpPost("email"), Authorize(Permissions.Edit)] public async Task<Result<AdmissionParentStaffContextDto>> EmailAsync(Guid applicationId, AdmissionParentEmailInput input) => Result.Success(await service.ChangeEmailAsync(applicationId, input));
	[HttpPost("corrections"), Authorize(Permissions.Edit)] public async Task<Result<AdmissionParentStaffContextDto>> CorrectionsAsync(Guid applicationId, AdmissionParentCorrectionInput input) => Result.Success(await service.RequestCorrectionsAsync(applicationId, input));
	[HttpPost("revoke"), Authorize(Permissions.Edit)] public async Task<Result<AdmissionParentStaffContextDto>> RevokeAsync(Guid applicationId, AdmissionParentCommand input) => Result.Success(await service.RevokeAsync(applicationId, input));
	[HttpPost("takeover"), Authorize(Permissions.Edit)] public async Task<Result<AdmissionParentStaffContextDto>> TakeoverAsync(Guid applicationId, AdmissionParentCommand input) => Result.Success(await service.TakeoverAsync(applicationId, input));
	[HttpPost("apply"), Authorize(Permissions.Edit)] public async Task<Result<AdmissionParentStaffContextDto>> ApplyAsync(Guid applicationId, AdmissionParentApplyInput input) => Result.Success(await service.ApplyAsync(applicationId, input));
	[HttpPost("retry-delivery"), Authorize(Permissions.Edit)] public async Task<Result<AdmissionParentStaffContextDto>> RetryAsync(Guid applicationId, AdmissionParentCommand input) => Result.Success(await service.RetryDeliveryAsync(applicationId, input));
}
