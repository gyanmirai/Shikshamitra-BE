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

[ApiController, Authorize, Route("api/student/enrollment/admissions/intake-profiles")]
public class AdmissionIntakeProfilesController(IAdmissionIntakeProfileAppService service) : AbpController
{
	[HttpGet, Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public async Task<Result<List<AdmissionIntakeProfileDto>>> GetListAsync([FromQuery] AdmissionIntakeProfileKind? kind = null) =>
		Result.Success(await service.GetListAsync(kind));

	[HttpGet("counsellors"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public async Task<Result<List<AdmissionInquiryCounsellorDto>>> GetCounsellorsAsync(
		[FromQuery] string? filter = null, [FromQuery] bool counsellorOnly = false) =>
		Result.Success(await service.GetCounsellorsAsync(filter, counsellorOnly));

	[HttpPost, Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add)]
	public async Task<Result<AdmissionIntakeProfileDto>> CreateAsync([FromBody] SaveAdmissionIntakeProfileDto input) =>
		Result.Success(await service.CreateAsync(input));

	[HttpPut("{id:guid}"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add)]
	public async Task<Result<AdmissionIntakeProfileDto>> UpdateAsync(Guid id, [FromBody] SaveAdmissionIntakeProfileDto input) =>
		Result.Success(await service.UpdateAsync(id, input));
}
