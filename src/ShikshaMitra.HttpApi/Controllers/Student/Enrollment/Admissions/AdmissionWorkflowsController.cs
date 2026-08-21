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
[Route("api/student/enrollment/admissions/workflows")]
public class AdmissionWorkflowsController(IAdmissionWorkflowAppService appService) : AbpController
{
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public async Task<Result<PagedResultDto<AdmissionWorkflowDto>>> GetList([FromQuery] AdmissionWorkflowListInput input) => Result.Success(await appService.GetListAsync(input));
	[HttpGet("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public async Task<Result<AdmissionWorkflowDto>> Get(Guid id) => Result.Success(await appService.GetAsync(id));
	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Create)]
	public async Task<Result<AdmissionWorkflowDto>> Create(CreateUpdateAdmissionWorkflowDto input) => Result.Success(await appService.CreateAsync(input));
	[HttpPut("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Edit)]
	public async Task<Result<AdmissionWorkflowDto>> Update(Guid id, CreateUpdateAdmissionWorkflowDto input) => Result.Success(await appService.UpdateAsync(id, input));
	[HttpDelete("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Delete)]
	public async Task<Result> Delete(Guid id)
	{
		await appService.DeleteAsync(id);
		return Result.Success();
	}
	[HttpPost("{id:guid}/clone")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Clone)]
	public async Task<Result<AdmissionWorkflowDto>> Clone(Guid id, CloneAdmissionWorkflowDto input) => Result.Success(await appService.CloneAsync(id, input));
	[HttpPost("{id:guid}/new-version")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Clone)]
	public async Task<Result<AdmissionWorkflowDto>> CreateNewVersion(Guid id) => Result.Success(await appService.CreateNewVersionAsync(id));
	[HttpPost("{id:guid}/validate")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Validate)]
	public async Task<Result<AdmissionWorkflowValidationResultDto>> Validate(Guid id) => Result.Success(await appService.ValidateAsync(id));
	[HttpPost("{id:guid}/publish")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Publish)]
	public async Task<Result<AdmissionWorkflowDto>> Publish(Guid id, [FromQuery] string? concurrencyStamp = null) => Result.Success(await appService.PublishAsync(id, concurrencyStamp));
	[HttpPost("{id:guid}/archive")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Archive)]
	public async Task<Result<AdmissionWorkflowDto>> Archive(Guid id) => Result.Success(await appService.ArchiveAsync(id));
	[HttpGet("{id:guid}/versions")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public async Task<Result<System.Collections.Generic.List<AdmissionWorkflowDto>>> GetVersionHistory(Guid id) => Result.Success(await appService.GetVersionHistoryAsync(id));
	[HttpGet("{id:guid}/usage")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.ViewUsage)]
	public async Task<Result<AdmissionWorkflowUsageDto>> GetUsage(Guid id) => Result.Success(await appService.GetUsageAsync(id));
	[HttpGet("stage-types")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public async Task<Result<System.Collections.Generic.IReadOnlyList<AdmissionWorkflowStageTypeMetadataDto>>> GetStageTypes() => Result.Success(await appService.GetStageTypesAsync());
}
