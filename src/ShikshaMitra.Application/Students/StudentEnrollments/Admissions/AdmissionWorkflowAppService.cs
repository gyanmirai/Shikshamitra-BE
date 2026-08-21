using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionWorkflowAppService(
	AdmissionWorkflowReadService readService,
	AdmissionWorkflowCommandService commandService)
	: ApplicationService, IAdmissionWorkflowAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public Task<PagedResultDto<AdmissionWorkflowDto>> GetListAsync(AdmissionWorkflowListInput input) =>
		readService.GetListAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public Task<AdmissionWorkflowDto> GetAsync(Guid id) => readService.GetAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Create)]
	public Task<AdmissionWorkflowDto> CreateAsync(CreateUpdateAdmissionWorkflowDto input) =>
		commandService.CreateAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Edit)]
	public Task<AdmissionWorkflowDto> UpdateAsync(Guid id, CreateUpdateAdmissionWorkflowDto input) =>
		commandService.UpdateAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Delete)]
	public Task DeleteAsync(Guid id) => commandService.DeleteAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Clone)]
	public Task<AdmissionWorkflowDto> CloneAsync(Guid id, CloneAdmissionWorkflowDto input) =>
		commandService.CloneAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.CreateVersion)]
	public Task<AdmissionWorkflowDto> CreateNewVersionAsync(Guid id) =>
		commandService.CreateNewVersionAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Validate)]
	public Task<AdmissionWorkflowValidationResultDto> ValidateAsync(Guid id) =>
		commandService.ValidateAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Publish)]
	public Task<AdmissionWorkflowDto> PublishAsync(Guid id, string? concurrencyStamp = null) =>
		commandService.PublishAsync(id, concurrencyStamp);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Archive)]
	public Task<AdmissionWorkflowDto> ArchiveAsync(Guid id) => commandService.ArchiveAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public Task<List<AdmissionWorkflowDto>> GetVersionHistoryAsync(Guid id) =>
		readService.GetVersionHistoryAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.ViewUsage)]
	public Task<AdmissionWorkflowUsageDto> GetUsageAsync(Guid id) => readService.GetUsageAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public Task<IReadOnlyList<AdmissionWorkflowStageTypeMetadataDto>> GetStageTypesAsync() =>
		Task.FromResult(readService.GetStageTypes());
}
