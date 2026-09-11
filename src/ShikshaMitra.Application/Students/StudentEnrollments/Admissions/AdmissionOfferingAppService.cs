using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionOfferingAppService(
	AdmissionOfferingReadService readService,
	AdmissionOfferingCommandService commandService)
	: ApplicationService, IAdmissionOfferingAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public Task<PagedResultDto<AdmissionOfferingListDto>> GetListAsync(AdmissionOfferingListInput input) =>
		readService.GetListAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public Task<AdmissionOfferingDto> GetAsync(Guid id) => readService.GetAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Create)]
	public Task<AdmissionOfferingDto> CreateAsync(Guid admissionCycleId, CreateAdmissionOfferingDto input) =>
		commandService.CreateAsync(admissionCycleId, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Edit)]
	public Task<AdmissionOfferingDto> UpdateAsync(Guid id, UpdateAdmissionOfferingDto input) =>
		commandService.UpdateAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Activate)]
	public Task<AdmissionOfferingDto> ActivateAsync(Guid id, string concurrencyStamp) =>
		commandService.ActivateAsync(id, concurrencyStamp);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Deactivate)]
	public Task<AdmissionOfferingDto> DeactivateAsync(Guid id, string concurrencyStamp) =>
		commandService.DeactivateAsync(id, concurrencyStamp);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Delete)]
	public Task DeleteAsync(Guid id, string concurrencyStamp) => commandService.DeleteAsync(id, concurrencyStamp);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Clone)]
	public Task<AdmissionOfferingDto> CloneAsync(Guid id, CloneAdmissionOfferingDto input) =>
		commandService.CloneAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Validate)]
	public Task<AdmissionOfferingConfigurationValidationDto> ValidateAsync(ValidateAdmissionOfferingDto input) =>
		commandService.ValidateAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public Task<AdmissionOfferingAvailabilityDto> GetAvailabilityAsync(Guid id) =>
		readService.GetAvailabilityAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.BulkCreate)]
	public Task<BulkAdmissionOfferingResultDto> BulkCreateAsync(
		Guid admissionCycleId,
		BulkCreateAdmissionOfferingsDto input) => commandService.BulkCreateAsync(admissionCycleId, input);
}
