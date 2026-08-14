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
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Manage)]
	public async Task<Result<AdmissionWorkflowDto>> Create(CreateUpdateAdmissionWorkflowDto input) => Result.Success(await appService.CreateAsync(input));
	[HttpPut("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Manage)]
	public async Task<Result<AdmissionWorkflowDto>> Update(Guid id, CreateUpdateAdmissionWorkflowDto input) => Result.Success(await appService.UpdateAsync(id, input));
	[HttpPost("{id:guid}/clone")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Manage)]
	public async Task<Result<AdmissionWorkflowDto>> Clone(Guid id) => Result.Success(await appService.CloneAsync(id));
	[HttpPost("{id:guid}/validate")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public async Task<Result<AdmissionWorkflowValidationResultDto>> Validate(Guid id) => Result.Success(await appService.ValidateAsync(id));
	[HttpPost("{id:guid}/publish")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Publish)]
	public async Task<Result<AdmissionWorkflowDto>> Publish(Guid id) => Result.Success(await appService.PublishAsync(id));
}
