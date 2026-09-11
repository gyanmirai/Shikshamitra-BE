using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using Volo.Abp.AspNetCore.Mvc;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionSeats;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[ApiController]
[Authorize]
[Route("api/student/enrollment/admissions/seats")]
public class AdmissionSeatsController(IAdmissionSeatAppService service) : AbpController
{
	[HttpGet("applications/{applicationId:guid}/stages/{stageId:guid}"), Authorize(Permissions.View)]
	public async Task<Result<AdmissionSeatStageDto>> GetAsync(Guid applicationId, Guid stageId) =>
		Result.Success(await service.GetAsync(applicationId, stageId));
	[HttpPost("applications/{applicationId:guid}/stages/{stageId:guid}/reserve"), Authorize(Permissions.Reserve)]
	public async Task<Result<AdmissionSeatStageDto>> ReserveAsync(Guid applicationId, Guid stageId, ReserveAdmissionSeatDto input) =>
		Result.Success(await service.ReserveAsync(applicationId, stageId, input));
	[HttpPost("applications/{applicationId:guid}/stages/{stageId:guid}/confirm"), Authorize(Permissions.Confirm)]
	public async Task<Result<AdmissionSeatStageDto>> ConfirmAsync(Guid applicationId, Guid stageId, ConfirmAdmissionSeatDto input) =>
		Result.Success(await service.ConfirmAsync(applicationId, stageId, input));
	[HttpPost("applications/{applicationId:guid}/stages/{stageId:guid}/renew"), Authorize(Permissions.Renew)]
	public async Task<Result<AdmissionSeatStageDto>> RenewAsync(Guid applicationId, Guid stageId, RenewAdmissionSeatDto input) =>
		Result.Success(await service.RenewAsync(applicationId, stageId, input));
	[HttpPost("applications/{applicationId:guid}/stages/{stageId:guid}/release"), Authorize(Permissions.Release)]
	public async Task<Result<AdmissionSeatStageDto>> ReleaseAsync(Guid applicationId, Guid stageId, AdmissionSeatReasonDto input) =>
		Result.Success(await service.ReleaseAsync(applicationId, stageId, input));
	[HttpPost("applications/{applicationId:guid}/stages/{stageId:guid}/waitlist"), Authorize(Permissions.Waitlist)]
	public async Task<Result<AdmissionSeatStageDto>> WaitlistAsync(Guid applicationId, Guid stageId, AdmissionSeatReasonDto input) =>
		Result.Success(await service.WaitlistAsync(applicationId, stageId, input));
	[HttpPut("applications/{applicationId:guid}/stages/{stageId:guid}/capacity"), Authorize(Permissions.ManageCapacity)]
	public async Task<Result<AdmissionSeatStageDto>> UpdateCapacityAsync(Guid applicationId, Guid stageId, UpdateAdmissionSeatCapacityDto input) =>
		Result.Success(await service.UpdateCapacityAsync(applicationId, stageId, input));
}
