using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize, RemoteService(false)]
public class AdmissionCrmCycleConfigurationService(AdmissionCrmCycleConfigurationWorkflow workflow)
	: ApplicationService, IAdmissionCrmCycleConfigurationService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmCycleConfigurationDto> GetSchoolDefaultAsync() => workflow.GetSchoolDefaultAsync();
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmCycleConfigurationDto> UpdateSchoolDefaultAsync(UpdateAdmissionCrmCycleConfigurationDto input) => workflow.UpdateSchoolDefaultAsync(input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmCycleConfigurationDto> GetAsync(Guid yearId) => workflow.GetAsync(yearId);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmCycleConfigurationDto> UpdateAsync(Guid yearId, UpdateAdmissionCrmCycleConfigurationDto input) => workflow.UpdateAsync(yearId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmCycleConfigurationDto> LockAsync(Guid yearId, AdmissionCrmJourneyLockDto input) => workflow.LockAsync(yearId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmCycleConfigurationDto> UnlockAsync(Guid yearId, AdmissionCrmJourneyLockDto input) => workflow.UnlockAsync(yearId, input);
}

[Authorize, RemoteService(false)]
public class AdmissionCrmPipelineConfigurationService(AdmissionCrmPipelineConfigurationWorkflow workflow)
	: ApplicationService, IAdmissionCrmPipelineConfigurationService
{
	public Task<PagedResultDto<AdmissionCrmPipelineStageDto>> GetAsync(Guid yearId) => workflow.GetAsync(yearId);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmPipelineStageDto> CreateAsync(Guid yearId, CreateAdmissionCrmPipelineStageDto input) => workflow.CreateAsync(yearId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmPipelineStageDto> UpdateAsync(Guid id, UpdateAdmissionCrmPipelineStageDto input) => workflow.UpdateAsync(id, input);
}

[Authorize, RemoteService(false)]
public class AdmissionCrmInquiryFormTemplateService(AdmissionCrmInquiryFormTemplateWorkflow workflow)
	: ApplicationService, IAdmissionCrmInquiryFormTemplateService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmFormTemplateDto> CreateAsync(Guid yearId, CreateAdmissionCrmFormTemplateDto input) => workflow.CreateAsync(yearId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<PagedResultDto<AdmissionCrmFormTemplateDto>> GetListAsync(Guid yearId, GetAdmissionCrmFormTemplateListInput input) => workflow.GetListAsync(yearId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmFormTemplateDto> GetAsync(Guid id) => workflow.GetAsync(id);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmFormTemplateDto> UpdateAsync(Guid id, UpdateAdmissionCrmFormTemplateDto input) => workflow.UpdateAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmFormTemplateDto> UpdateStatusAsync(Guid id, UpdateAdmissionCrmFormTemplateStatusDto input) => workflow.UpdateStatusAsync(id, input);
}

[Authorize, RemoteService(false)]
public class AdmissionCrmMessageTemplateService(AdmissionCrmMessageTemplateWorkflow workflow)
	: ApplicationService, IAdmissionCrmMessageTemplateService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public Task<AdmissionCrmMessageTemplateDto> CreateAsync(Guid yearId, CreateAdmissionCrmMessageTemplateDto input) => workflow.CreateAsync(yearId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public Task<PagedResultDto<AdmissionCrmMessageTemplateDto>> GetListAsync(Guid yearId, GetAdmissionCrmMessageTemplateListInput input) => workflow.GetListAsync(yearId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public Task<AdmissionCrmMessageTemplateDto> GetAsync(Guid id) => workflow.GetAsync(id);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public Task<AdmissionCrmMessageTemplateDto> UpdateAsync(Guid id, UpdateAdmissionCrmMessageTemplateDto input) => workflow.UpdateAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public Task<AdmissionCrmMessageTemplateDto> UpdateStatusAsync(Guid id, UpdateAdmissionCrmMessageTemplateStatusDto input) => workflow.UpdateStatusAsync(id, input);
}
