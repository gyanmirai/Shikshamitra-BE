using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration), RemoteService(false)]
public class AdmissionCrmConfigurationReferenceService(AdmissionCrmConfigurationReferenceWorkflow workflow)
	: ApplicationService, IAdmissionCrmConfigurationReferenceService
{
	public Task<AdmissionCrmConfigurationReferenceResultDto> GetAsync(Guid yearId,
		AdmissionCrmConfigurationReferenceKind kind, string code) => workflow.GetAsync(yearId, kind, code);
}

[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration), RemoteService(false)]
public class AdmissionCrmConfigurationImpactService(AdmissionCrmConfigurationImpactWorkflow workflow)
	: ApplicationService, IAdmissionCrmConfigurationImpactService
{
	public Task<AdmissionCrmConfigurationImpactPreviewDto> PreviewAsync(Guid yearId,
		PreviewAdmissionCrmConfigurationImpactDto input) => workflow.PreviewAsync(yearId, input);
}

[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration), RemoteService(false)]
public class AdmissionCrmConfigurationCloneService(AdmissionCrmConfigurationCloneWorkflow workflow)
	: ApplicationService, IAdmissionCrmConfigurationCloneService
{
	public Task<AdmissionCrmConfigurationClonePreviewDto> PreviewAsync(PreviewAdmissionCrmConfigurationCloneDto input) =>
		workflow.PreviewAsync(input);
	[UnitOfWork(IsDisabled = true)]
	public Task<AdmissionCrmConfigurationCloneResultDto> ConfirmAsync(ConfirmAdmissionCrmConfigurationCloneDto input) =>
		workflow.ConfirmAsync(input);
}
