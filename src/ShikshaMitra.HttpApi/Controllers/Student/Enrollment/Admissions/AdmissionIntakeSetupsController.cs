using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Student.Enrollment.Admissions;

[ApiController, Authorize, Route("api/student/enrollment/admissions/intake-setups")]
public class AdmissionIntakeSetupsController(IAdmissionIntakeSetupAppService service) : AbpController
{
	[HttpGet, Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public async Task<Result<List<AdmissionIntakeSetupDto>>> GetListAsync([FromQuery] bool includeArchived = false) =>
		Result.Success(await service.GetListAsync(includeArchived));

	[HttpPost, Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add)]
	public async Task<Result<AdmissionIntakeSetupDto>> CreateAsync([FromBody] CreateAdmissionIntakeSetupDto input) =>
		Result.Success(await service.CreateAsync(input));

	[HttpPut("{id:guid}"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add)]
	public async Task<Result<AdmissionIntakeSetupDto>> UpdateAsync(Guid id,
		[FromBody] UpdateAdmissionIntakeSetupDto input) =>
		Result.Success(await service.UpdateAsync(id, input));

	[HttpGet("{id:guid}/usage"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public async Task<Result<AdmissionIntakeSetupUsageDto>> GetUsageAsync(Guid id) =>
		Result.Success(await service.GetUsageAsync(id));

	[HttpPost("{id:guid}/archive"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add)]
	public async Task<Result<AdmissionIntakeSetupDto>> ArchiveAsync(Guid id,
		[FromBody] ArchiveAdmissionIntakeSetupDto input) =>
		Result.Success(await service.ArchiveAsync(id, input));

	[HttpPost("{id:guid}/restore"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add)]
	public async Task<Result<AdmissionIntakeSetupDto>> RestoreAsync(Guid id,
		[FromBody] RestoreAdmissionIntakeSetupDto input) =>
		Result.Success(await service.RestoreAsync(id, input));
}
