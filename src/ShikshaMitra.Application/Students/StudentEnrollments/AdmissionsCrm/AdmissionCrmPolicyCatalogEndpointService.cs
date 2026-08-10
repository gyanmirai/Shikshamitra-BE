using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmPolicyCatalogService(
	AdmissionCrmPolicyCatalogWorkflow workflow) : ApplicationService, IAdmissionCrmPolicyCatalogService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManagePolicyPacks)]
	public Task<AdmissionCrmPolicyPackDto> CreatePolicyPackAsync(CreateAdmissionCrmPolicyPackDto input) =>
		workflow.CreatePolicyPackAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewPolicyPacks)]
	public Task<PagedResultDto<AdmissionCrmPolicyPackDto>> GetPolicyPacksAsync(GetAdmissionCrmPolicyPackListInput input) =>
		workflow.GetPolicyPacksAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManagePolicyPacks)]
	public Task<AdmissionCrmPolicyBaselineInstallResultDto> InstallOfficialBaselineDraftsAsync() =>
		workflow.InstallOfficialBaselineDraftsAsync();

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManagePolicyPacks)]
	public Task<AdmissionCrmPolicyPackDto> SubmitPolicyPackAsync(Guid id) => workflow.SubmitPolicyPackAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ApprovePolicyPacks)]
	public Task<AdmissionCrmPolicyPackDto> ApprovePolicyPackAsync(Guid id) => workflow.ApprovePolicyPackAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ApprovePolicyPacks)]
	public Task<AdmissionCrmPolicyPackDto> PublishPolicyPackAsync(Guid id) => workflow.PublishPolicyPackAsync(id);
}
