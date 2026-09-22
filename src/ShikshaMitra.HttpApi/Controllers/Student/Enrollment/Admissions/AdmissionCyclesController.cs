using System;
using System.Collections.Generic;
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
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	public async Task<Result<AdmissionCycleDto>> Create([FromBody] CreateUpdateAdmissionCycleDto input) =>
		Result.Success(await appService.CreateAsync(input));

	[HttpPut("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	public async Task<Result<AdmissionCycleDto>> Update(Guid id, [FromBody] CreateUpdateAdmissionCycleDto input) =>
		Result.Success(await appService.UpdateAsync(id, input));

	[HttpPost("{id:guid}/publish")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	public async Task<Result<AdmissionCycleDto>> Publish(Guid id, [FromQuery] string? concurrencyStamp = null) =>
		Result.Success(await appService.PublishAsync(id, concurrencyStamp));

	[HttpPost("{id:guid}/close")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Delete)]
	public async Task<Result<AdmissionCycleDto>> Close(Guid id, [FromBody] AdmissionCycleActionDto input) =>
		Result.Success(await appService.CloseAsync(id, input));

	[HttpPost("{id:guid}/reopen")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	public async Task<Result<AdmissionCycleDto>> Reopen(Guid id, [FromBody] ReopenAdmissionCycleDto input) =>
		Result.Success(await appService.ReopenAsync(id, input));

	[HttpPost("{id:guid}/archive")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Delete)]
	public async Task<Result<AdmissionCycleDto>> Archive(Guid id, [FromBody] AdmissionCycleActionDto input) =>
		Result.Success(await appService.ArchiveAsync(id, input));

	[HttpDelete("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Delete)]
	public async Task<Result> Delete(Guid id, [FromQuery] string? concurrencyStamp = null)
	{
		await appService.DeleteAsync(id, concurrencyStamp);
		return Result.Success();
	}

	[HttpPost("clone")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	public async Task<Result<AdmissionCycleDto>> Clone([FromBody] CloneAdmissionCycleDto input) =>
		Result.Success(await appService.CloneAsync(input));

	[HttpPut("{id:guid}/window")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	public async Task<Result<AdmissionCycleDto>> UpdateWindow(Guid id, [FromBody] UpdateAdmissionCycleWindowDto input) =>
		Result.Success(await appService.UpdateWindowAsync(id, input));

	[HttpPut("{id:guid}/schedule")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	public async Task<Result<AdmissionCycleDto>> Schedule(Guid id, [FromBody] ScheduleAdmissionCycleDto input) =>
		Result.Success(await appService.ScheduleAsync(id, input));

	[HttpPost("{id:guid}/validate")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	public async Task<Result<AdmissionCycleValidationDto>> Validate(Guid id) =>
		Result.Success(await appService.ValidateAsync(id));

	[HttpGet("{id:guid}/impact")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.View)]
	public async Task<Result<AdmissionCycleImpactDto>> GetImpact(Guid id) =>
		Result.Success(await appService.GetImpactAsync(id));

	[HttpGet("{id:guid}/history")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.View)]
	public async Task<Result<List<AdmissionCycleHistoryDto>>> GetHistory(Guid id) =>
		Result.Success(await appService.GetHistoryAsync(id));
}
