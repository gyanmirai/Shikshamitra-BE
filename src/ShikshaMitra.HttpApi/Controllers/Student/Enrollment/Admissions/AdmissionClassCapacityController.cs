using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[ApiController]
[Authorize]
[Route("api/student/enrollment/admissions/offerings/{offeringId:guid}/class-capacity")]
public class AdmissionClassCapacityController(IAdmissionClassCapacityAppService service) : AbpController
{
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public async Task<Result<AdmissionClassCapacityDto>> Get(Guid offeringId) =>
		Result.Success(await service.GetForOfferingAsync(offeringId));

	[HttpGet("rooms")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public async Task<Result<List<AdmissionRoomChoiceDto>>> GetRooms(Guid offeringId, [FromQuery] string? search) =>
		Result.Success(await service.GetRoomChoicesAsync(offeringId, search));

	[HttpPut]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add)]
	public async Task<Result<AdmissionClassCapacityDto>> Update(
		Guid offeringId, [FromBody] UpdateAdmissionClassRoomsDto input) =>
		Result.Success(await service.UpdateForOfferingAsync(offeringId, input));
}
