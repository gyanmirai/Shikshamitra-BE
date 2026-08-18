using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[ApiController]
[Authorize]
[Route("api/student/enrollment/admissions/offerings")]
public class AdmissionOfferingsController(IAdmissionOfferingAppService appService) : AbpController
{
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public async Task<Result<PagedResultDto<AdmissionOfferingListDto>>> GetList(
		[FromQuery] AdmissionOfferingListInput input) => Result.Success(await appService.GetListAsync(input));

	[HttpGet("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public async Task<Result<AdmissionOfferingDto>> Get(Guid id) => Result.Success(await appService.GetAsync(id));

	[HttpPost("cycles/{admissionCycleId:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Create)]
	public async Task<Result<AdmissionOfferingDto>> Create(
		Guid admissionCycleId,
		[FromBody] CreateAdmissionOfferingDto input) =>
		Result.Success(await appService.CreateAsync(admissionCycleId, input));

	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Create)]
	public async Task<Result<AdmissionOfferingDto>> CreateCompatibility(
		[FromQuery] Guid admissionCycleId,
		[FromBody] CreateAdmissionOfferingDto input) =>
		Result.Success(await appService.CreateAsync(admissionCycleId, input));

	[HttpPut("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Edit)]
	public async Task<Result<AdmissionOfferingDto>> Update(Guid id, [FromBody] UpdateAdmissionOfferingDto input) =>
		Result.Success(await appService.UpdateAsync(id, input));

	[HttpPost("{id:guid}/activate")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Activate)]
	public async Task<Result<AdmissionOfferingDto>> Activate(Guid id, [FromQuery] string concurrencyStamp) =>
		Result.Success(await appService.ActivateAsync(id, concurrencyStamp));

	[HttpPost("{id:guid}/deactivate")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Deactivate)]
	public async Task<Result<AdmissionOfferingDto>> Deactivate(Guid id, [FromQuery] string concurrencyStamp) =>
		Result.Success(await appService.DeactivateAsync(id, concurrencyStamp));

	[HttpDelete("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Delete)]
	public async Task<Result> Delete(Guid id, [FromQuery] string concurrencyStamp)
	{
		await appService.DeleteAsync(id, concurrencyStamp);
		return Result.Success();
	}

	[HttpPost("{id:guid}/clone")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Clone)]
	public async Task<Result<AdmissionOfferingDto>> Clone(Guid id, [FromBody] CloneAdmissionOfferingDto input) =>
		Result.Success(await appService.CloneAsync(id, input));

	[HttpPost("validate")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Validate)]
	public async Task<Result<AdmissionOfferingConfigurationValidationDto>> Validate(
		[FromBody] ValidateAdmissionOfferingDto input) => Result.Success(await appService.ValidateAsync(input));

	[HttpGet("{id:guid}/availability")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public async Task<Result<AdmissionOfferingAvailabilityDto>> GetAvailability(Guid id) =>
		Result.Success(await appService.GetAvailabilityAsync(id));

	[HttpPost("cycles/{admissionCycleId:guid}/bulk")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.BulkCreate)]
	public async Task<Result<BulkAdmissionOfferingResultDto>> BulkCreate(
		Guid admissionCycleId,
		[FromBody] BulkCreateAdmissionOfferingsDto input) =>
		Result.Success(await appService.BulkCreateAsync(admissionCycleId, input));
}
