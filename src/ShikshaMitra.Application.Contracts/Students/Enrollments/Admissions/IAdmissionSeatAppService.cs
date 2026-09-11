using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionSeatAppService : IApplicationService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionSeats.View)]
	Task<AdmissionSeatStageDto> GetAsync(Guid applicationId, Guid stageId);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionSeats.Reserve)]
	Task<AdmissionSeatStageDto> ReserveAsync(Guid applicationId, Guid stageId, ReserveAdmissionSeatDto input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionSeats.Confirm)]
	Task<AdmissionSeatStageDto> ConfirmAsync(Guid applicationId, Guid stageId, ConfirmAdmissionSeatDto input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionSeats.Renew)]
	Task<AdmissionSeatStageDto> RenewAsync(Guid applicationId, Guid stageId, RenewAdmissionSeatDto input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionSeats.Release)]
	Task<AdmissionSeatStageDto> ReleaseAsync(Guid applicationId, Guid stageId, AdmissionSeatReasonDto input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionSeats.Waitlist)]
	Task<AdmissionSeatStageDto> WaitlistAsync(Guid applicationId, Guid stageId, AdmissionSeatReasonDto input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionSeats.ManageCapacity)]
	Task<AdmissionSeatStageDto> UpdateCapacityAsync(Guid applicationId, Guid stageId, UpdateAdmissionSeatCapacityDto input);
}
