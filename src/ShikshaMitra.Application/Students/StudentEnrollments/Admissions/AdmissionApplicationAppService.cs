using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionApplicationAppService(
	AdmissionApplicationReadService readService,
	AdmissionApplicationLifecycleService lifecycleService,
	AdmissionApplicationWorkflowReadService workflowReadService,
	AdmissionApplicationWorkflowService workflowService,
	AdmissionApplicationFormService formService)
	: ApplicationService, IAdmissionApplicationAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<PagedResultDto<AdmissionApplicationDto>> GetListAsync(AdmissionApplicationListInput input) =>
		readService.GetListAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<AdmissionApplicationDto> GetAsync(Guid id) => readService.GetAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Start)]
	public Task<AdmissionApplicationDto> StartAsync(Guid applicantId, Guid offeringId, Guid? inquiryId = null) =>
		lifecycleService.StartAsync(applicantId, offeringId, inquiryId);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<AdmissionApplicationWorkflowDto> GetWorkflowAsync(Guid id) =>
		workflowReadService.GetAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Advance)]
	public async Task<AdmissionApplicationWorkflowDto> AdvanceWorkflowAsync(
		Guid id,
		AdvanceAdmissionWorkflowDto input)
	{
		if (input.AdminOverride)
			await AuthorizationService.CheckAsync(
				ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.OverrideWorkflow);
		else if (input.SkipCurrentStage)
			await AuthorizationService.CheckAsync(
				ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.SkipStage);
		return await workflowService.AdvanceAsync(id, input);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<AdmissionApplicationFormDto> GetFormAsync(Guid id) => formService.GetAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Edit)]
	public Task<AdmissionApplicationFormDto> UpdateFormValuesAsync(
		Guid id,
		UpdateAdmissionApplicationFormValuesDto input) => formService.UpdateAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Submit)]
	public Task<AdmissionApplicationSubmitResultDto> SubmitAsync(Guid id) => formService.SubmitAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Reopen)]
	public Task<AdmissionApplicationFormDto> ReopenFormAsync(Guid id) => formService.ReopenAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<List<AdmissionFormChangeHistoryDto>> GetFormHistoryAsync(Guid id) => formService.GetHistoryAsync(id);
}
