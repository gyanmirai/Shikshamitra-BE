using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmRegistrationTemplateService : IApplicationService
{
	Task<PagedResultDto<AdmissionCrmRegistrationTemplateDto>> GetTemplatesAsync(Guid academicYearId, GetAdmissionCrmRegistrationTemplateListInput input);
	Task<AdmissionCrmRegistrationTemplateDto> GetTemplateAsync(Guid id);
	Task<AdmissionCrmRegistrationTemplateDto> CreateTemplateAsync(Guid academicYearId, CreateAdmissionCrmRegistrationTemplateDto input);
	Task<AdmissionCrmRegistrationTemplateDto> UpdateTemplateAsync(Guid id, UpdateAdmissionCrmRegistrationTemplateDto input);
	Task<AdmissionCrmRegistrationTemplateDto> PublishTemplateAsync(Guid id);
	Task<AdmissionCrmRegistrationTemplateDto> CreateTemplateVersionAsync(Guid id);
	Task<AdmissionCrmRegistrationTemplateDto> RetireTemplateAsync(Guid id);
}

public interface IAdmissionCrmRegistrationNumberSeriesService : IApplicationService
{
	Task<PagedResultDto<AdmissionCrmFormNumberSeriesDto>> GetFormNumberSeriesAsync(Guid academicYearId);
	Task<AdmissionCrmFormNumberSeriesDto> CreateFormNumberSeriesAsync(Guid academicYearId, AdmissionCrmFormNumberSeriesUpsertDto input);
	Task<AdmissionCrmFormNumberSeriesDto> UpdateFormNumberSeriesAsync(Guid id, AdmissionCrmFormNumberSeriesUpsertDto input);
}

public interface IAdmissionCrmRegistrationIssuanceService : IApplicationService
{
	Task<AdmissionCrmRegistrationApplicationDto> IssueAsync(Guid leadId, IssueAdmissionCrmRegistrationApplicationDto input);
}

public interface IAdmissionCrmRegistrationQueryService : IApplicationService
{
	Task<AdmissionCrmRegistrationApplicationDto> GetApplicationAsync(Guid id);
	Task<AdmissionCrmRegistrationApplicationDto> GetCurrentApplicationByLeadAsync(Guid leadId);
}

public interface IAdmissionCrmRegistrationResponseService : IApplicationService
{
	Task<AdmissionCrmRegistrationApplicationDto> SaveStaffResponsesAsync(Guid id, SaveAdmissionCrmRegistrationResponsesDto input);
	Task<AdmissionCrmRegistrationApplicationDto> SubmitStaffResponsesAsync(Guid id, SaveAdmissionCrmRegistrationResponsesDto input);
	Task<AdmissionCrmRegistrationApplicationDto> ReturnForCorrectionAsync(Guid id, ReturnAdmissionCrmRegistrationForCorrectionDto input);
	Task<AdmissionCrmRegistrationApplicationDto> VerifyAsync(Guid id);
}

public interface IAdmissionCrmRegistrationLinkService : IApplicationService
{
	Task<AdmissionCrmRegistrationApplicationDto> ExtendAsync(Guid id, ExtendAdmissionCrmRegistrationLinkDto input);
	Task<AdmissionCrmRegistrationApplicationDto> RevokeAsync(Guid id, AdmissionCrmRegistrationReasonDto input);
	Task<AdmissionCrmRegistrationApplicationDto> ReissueAsync(Guid id, AdmissionCrmRegistrationReissueDto input);
}

public interface IAdmissionCrmRegistrationFeeService : IApplicationService
{
	Task<AdmissionCrmRegistrationApplicationDto> RecordPaymentReferenceAsync(Guid id, RecordAdmissionCrmRegistrationPaymentReferenceDto input);
	Task<AdmissionCrmRegistrationApplicationDto> WaiveFeeAsync(Guid id, AdmissionCrmRegistrationReasonDto input);
}
