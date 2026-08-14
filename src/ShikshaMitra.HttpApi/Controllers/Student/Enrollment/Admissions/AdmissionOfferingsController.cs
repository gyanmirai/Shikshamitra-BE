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
	public async Task<Result<PagedResultDto<AdmissionOfferingDto>>> GetList([FromQuery] AdmissionOfferingListInput input) =>
		Result.Success(await appService.GetListAsync(input));

	[HttpGet("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public async Task<Result<AdmissionOfferingDto>> Get(Guid id) => Result.Success(await appService.GetAsync(id));

	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Manage)]
	public async Task<Result<AdmissionOfferingDto>> Create([FromQuery] Guid admissionCycleId, [FromBody] CreateUpdateAdmissionOfferingDto input) =>
		Result.Success(await appService.CreateAsync(admissionCycleId, input));

	[HttpPut("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Manage)]
	public async Task<Result<AdmissionOfferingDto>> Update(Guid id, [FromBody] CreateUpdateAdmissionOfferingDto input) =>
		Result.Success(await appService.UpdateAsync(id, input));

	[HttpPost("{id:guid}/active")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Manage)]
	public async Task<Result<AdmissionOfferingDto>> SetActive(Guid id, [FromQuery] bool isActive) =>
		Result.Success(await appService.SetActiveAsync(id, isActive));
}
