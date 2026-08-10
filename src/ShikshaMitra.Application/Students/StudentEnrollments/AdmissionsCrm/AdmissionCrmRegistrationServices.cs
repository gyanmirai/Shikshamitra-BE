using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize, RemoteService(false)]
public class AdmissionCrmRegistrationTemplateService(AdmissionCrmRegistrationTemplateWorkflow workflow)
	: ApplicationService, IAdmissionCrmRegistrationTemplateService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<PagedResultDto<AdmissionCrmRegistrationTemplateDto>> GetTemplatesAsync(Guid yearId, GetAdmissionCrmRegistrationTemplateListInput input) => workflow.GetListAsync(yearId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<AdmissionCrmRegistrationTemplateDto> GetTemplateAsync(Guid id) => workflow.GetAsync(id);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRegistrationTemplates)]
	public Task<AdmissionCrmRegistrationTemplateDto> CreateTemplateAsync(Guid yearId, CreateAdmissionCrmRegistrationTemplateDto input) => workflow.CreateAsync(yearId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRegistrationTemplates)]
	public Task<AdmissionCrmRegistrationTemplateDto> UpdateTemplateAsync(Guid id, UpdateAdmissionCrmRegistrationTemplateDto input) => workflow.UpdateAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.PublishRegistrationTemplates)]
	public Task<AdmissionCrmRegistrationTemplateDto> PublishTemplateAsync(Guid id) => workflow.PublishAsync(id);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRegistrationTemplates)]
	public Task<AdmissionCrmRegistrationTemplateDto> CreateTemplateVersionAsync(Guid id) => workflow.CreateVersionAsync(id);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.PublishRegistrationTemplates)]
	public Task<AdmissionCrmRegistrationTemplateDto> RetireTemplateAsync(Guid id) => workflow.RetireAsync(id);
}

[Authorize, RemoteService(false)]
public class AdmissionCrmRegistrationNumberSeriesService(AdmissionCrmRegistrationNumberSeriesWorkflow workflow)
	: ApplicationService, IAdmissionCrmRegistrationNumberSeriesService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<PagedResultDto<AdmissionCrmFormNumberSeriesDto>> GetFormNumberSeriesAsync(Guid yearId) => workflow.GetListAsync(yearId);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageFormNumberSeries)]
	public Task<AdmissionCrmFormNumberSeriesDto> CreateFormNumberSeriesAsync(Guid yearId, AdmissionCrmFormNumberSeriesUpsertDto input) => workflow.CreateAsync(yearId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageFormNumberSeries)]
	public Task<AdmissionCrmFormNumberSeriesDto> UpdateFormNumberSeriesAsync(Guid id, AdmissionCrmFormNumberSeriesUpsertDto input) => workflow.UpdateAsync(id, input);
}

[Authorize, RemoteService(false)]
public class AdmissionCrmRegistrationIssuanceService(AdmissionCrmRegistrationIssuanceWorkflow workflow)
	: ApplicationService, IAdmissionCrmRegistrationIssuanceService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.IssueRegistrationForm)]
	public Task<AdmissionCrmRegistrationApplicationDto> IssueAsync(Guid leadId, IssueAdmissionCrmRegistrationApplicationDto input) => workflow.IssueAsync(leadId, input);
}

[Authorize, RemoteService(false)]
public class AdmissionCrmRegistrationQueryService(AdmissionCrmRegistrationQueryWorkflow workflow)
	: ApplicationService, IAdmissionCrmRegistrationQueryService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<AdmissionCrmRegistrationApplicationDto> GetApplicationAsync(Guid id) => workflow.GetAsync(id);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<AdmissionCrmRegistrationApplicationDto> GetCurrentApplicationByLeadAsync(Guid leadId) => workflow.GetCurrentByLeadAsync(leadId);
}

[Authorize, RemoteService(false)]
public class AdmissionCrmRegistrationResponseService(AdmissionCrmRegistrationResponseWorkflow workflow)
	: ApplicationService, IAdmissionCrmRegistrationResponseService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.IssueRegistrationForm)]
	public Task<AdmissionCrmRegistrationApplicationDto> SaveStaffResponsesAsync(Guid id, SaveAdmissionCrmRegistrationResponsesDto input) => workflow.SaveAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.IssueRegistrationForm)]
	public Task<AdmissionCrmRegistrationApplicationDto> SubmitStaffResponsesAsync(Guid id, SaveAdmissionCrmRegistrationResponsesDto input) => workflow.SubmitAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.VerifyRegistration)]
	public Task<AdmissionCrmRegistrationApplicationDto> ReturnForCorrectionAsync(Guid id, ReturnAdmissionCrmRegistrationForCorrectionDto input) => workflow.ReturnAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.VerifyRegistration)]
	public Task<AdmissionCrmRegistrationApplicationDto> VerifyAsync(Guid id) => workflow.VerifyAsync(id);
}

[Authorize, RemoteService(false)]
public class AdmissionCrmRegistrationLinkService(AdmissionCrmRegistrationLinkWorkflow workflow)
	: ApplicationService, IAdmissionCrmRegistrationLinkService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRegistrationLinks)]
	public Task<AdmissionCrmRegistrationApplicationDto> ExtendAsync(Guid id, ExtendAdmissionCrmRegistrationLinkDto input) => workflow.ExtendAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRegistrationLinks)]
	public Task<AdmissionCrmRegistrationApplicationDto> RevokeAsync(Guid id, AdmissionCrmRegistrationReasonDto input) => workflow.RevokeAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRegistrationLinks)]
	public Task<AdmissionCrmRegistrationApplicationDto> ReissueAsync(Guid id, AdmissionCrmRegistrationReissueDto input) => workflow.ReissueAsync(id, input);
}

[Authorize, RemoteService(false)]
public class AdmissionCrmRegistrationFeeService(AdmissionCrmRegistrationFeeWorkflow workflow)
	: ApplicationService, IAdmissionCrmRegistrationFeeService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.IssueRegistrationForm)]
	public Task<AdmissionCrmRegistrationApplicationDto> RecordPaymentReferenceAsync(Guid id, RecordAdmissionCrmRegistrationPaymentReferenceDto input) => workflow.RecordReferenceAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.WaiveRegistrationFee)]
	public Task<AdmissionCrmRegistrationApplicationDto> WaiveFeeAsync(Guid id, AdmissionCrmRegistrationReasonDto input) => workflow.WaiveAsync(id, input);
}
