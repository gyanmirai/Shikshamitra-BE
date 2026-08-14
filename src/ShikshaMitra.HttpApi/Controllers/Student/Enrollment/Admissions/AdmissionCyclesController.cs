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
[Route("api/student/enrollment/admissions/cycles")]
public class AdmissionCyclesController(IAdmissionCycleAppService appService) : AbpController
{
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.View)]
	public async Task<Result<PagedResultDto<AdmissionCycleDto>>> GetList([FromQuery] AdmissionCycleListInput input) =>
		Result.Success(await appService.GetListAsync(input));

	[HttpGet("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.View)]
	public async Task<Result<AdmissionCycleDto>> Get(Guid id) => Result.Success(await appService.GetAsync(id));

	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Create)]
	public async Task<Result<AdmissionCycleDto>> Create([FromBody] CreateUpdateAdmissionCycleDto input) =>
		Result.Success(await appService.CreateAsync(input));

	[HttpPut("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Edit)]
	public async Task<Result<AdmissionCycleDto>> Update(Guid id, [FromBody] CreateUpdateAdmissionCycleDto input) =>
		Result.Success(await appService.UpdateAsync(id, input));

	[HttpPost("{id:guid}/publish")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Publish)]
	public async Task<Result<AdmissionCycleDto>> Publish(Guid id) => Result.Success(await appService.PublishAsync(id));

	[HttpPost("{id:guid}/close")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Close)]
	public async Task<Result<AdmissionCycleDto>> Close(Guid id) => Result.Success(await appService.CloseAsync(id));

	[HttpPost("{id:guid}/archive")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Archive)]
	public async Task<Result<AdmissionCycleDto>> Archive(Guid id) => Result.Success(await appService.ArchiveAsync(id));
}
