using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionSeats;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionSeatAppService(AdmissionSeatReadService reader, AdmissionSeatCommandService commands)
	: ApplicationService, IAdmissionSeatAppService
{
	[Authorize(Permissions.View)]
	public Task<AdmissionSeatStageDto> GetAsync(Guid applicationId, Guid stageId) => reader.GetAsync(applicationId, stageId);

	[Authorize(Permissions.Reserve)]
	public async Task<AdmissionSeatStageDto> ReserveAsync(Guid applicationId, Guid stageId, ReserveAdmissionSeatDto input)
	{
		if (input.OverrideCapacity) await AuthorizationService.CheckAsync(Permissions.OverrideCapacity);
		return await commands.ReserveAsync(applicationId, stageId, input);
	}

	[Authorize(Permissions.Confirm)]
	public Task<AdmissionSeatStageDto> ConfirmAsync(Guid applicationId, Guid stageId, ConfirmAdmissionSeatDto input) =>
		commands.ConfirmAsync(applicationId, stageId, input);

	[Authorize(Permissions.Renew)]
	public Task<AdmissionSeatStageDto> RenewAsync(Guid applicationId, Guid stageId, RenewAdmissionSeatDto input) =>
		commands.RenewAsync(applicationId, stageId, input);

	[Authorize(Permissions.Release)]
	public Task<AdmissionSeatStageDto> ReleaseAsync(Guid applicationId, Guid stageId, AdmissionSeatReasonDto input) =>
		commands.ReleaseAsync(applicationId, stageId, input);

	[Authorize(Permissions.Waitlist)]
	public Task<AdmissionSeatStageDto> WaitlistAsync(Guid applicationId, Guid stageId, AdmissionSeatReasonDto input) =>
		commands.WaitlistAsync(applicationId, stageId, input);

	[Authorize(Permissions.ManageCapacity)]
	public async Task<AdmissionSeatStageDto> UpdateCapacityAsync(
		Guid applicationId, Guid stageId, UpdateAdmissionSeatCapacityDto input)
	{
		if (input.AllowBelowOccupied) await AuthorizationService.CheckAsync(Permissions.OverrideCapacity);
		return await commands.UpdateCapacityAsync(applicationId, stageId, input);
	}
}
