using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmRegistrationAppService : IApplicationService
{
	Task<PagedResultDto<AdmissionCrmRegistrationTemplateDto>> GetTemplatesAsync(Guid academicYearId, GetAdmissionCrmRegistrationTemplateListInput input);
	Task<AdmissionCrmRegistrationTemplateDto> GetTemplateAsync(Guid id);
	Task<AdmissionCrmRegistrationTemplateDto> CreateTemplateAsync(Guid academicYearId, CreateAdmissionCrmRegistrationTemplateDto input);
	Task<AdmissionCrmRegistrationTemplateDto> UpdateTemplateAsync(Guid id, UpdateAdmissionCrmRegistrationTemplateDto input);
	Task<AdmissionCrmRegistrationTemplateDto> PublishTemplateAsync(Guid id);
	Task<AdmissionCrmRegistrationTemplateDto> CreateTemplateVersionAsync(Guid id);
	Task<AdmissionCrmRegistrationTemplateDto> RetireTemplateAsync(Guid id);
	Task<PagedResultDto<AdmissionCrmFormNumberSeriesDto>> GetFormNumberSeriesAsync(Guid academicYearId);
	Task<AdmissionCrmFormNumberSeriesDto> CreateFormNumberSeriesAsync(Guid academicYearId, AdmissionCrmFormNumberSeriesUpsertDto input);
	Task<AdmissionCrmFormNumberSeriesDto> UpdateFormNumberSeriesAsync(Guid id, AdmissionCrmFormNumberSeriesUpsertDto input);
	Task<AdmissionCrmRegistrationApplicationDto> IssueAsync(Guid leadId, IssueAdmissionCrmRegistrationApplicationDto input);
	Task<AdmissionCrmRegistrationApplicationDto> IssueDirectAsync(
		Guid leadId,
		IssueAdmissionCrmRegistrationApplicationDto input);
	Task EnsureStandardIssuanceEligibleAsync(Guid leadId, Guid? playbookId = null);
	Task<bool> IssueApplicableFromAutomationAsync(Guid leadId, AdmissionInquiryStage stage);
	Task<AdmissionCrmRegistrationApplicationDto> GetApplicationAsync(Guid id);
	Task<AdmissionCrmRegistrationApplicationDto> GetCurrentApplicationByLeadAsync(Guid leadId);
	Task<AdmissionCrmRegistrationApplicationDto> SaveStaffResponsesAsync(
		Guid id,
		SaveAdmissionCrmRegistrationResponsesDto input);
	Task<AdmissionCrmRegistrationApplicationDto> SubmitStaffResponsesAsync(
		Guid id,
		SaveAdmissionCrmRegistrationResponsesDto input);
	Task<AdmissionCrmRegistrationApplicationDto> ReturnForCorrectionAsync(Guid id, ReturnAdmissionCrmRegistrationForCorrectionDto input);
	Task<AdmissionCrmRegistrationApplicationDto> VerifyAsync(Guid id);
	Task<AdmissionCrmRegistrationApplicationDto> ExtendAsync(Guid id, ExtendAdmissionCrmRegistrationLinkDto input);
	Task<AdmissionCrmRegistrationApplicationDto> RevokeAsync(Guid id, AdmissionCrmRegistrationReasonDto input);
	Task<AdmissionCrmRegistrationApplicationDto> ReissueAsync(Guid id, AdmissionCrmRegistrationReissueDto input);
	Task<AdmissionCrmRegistrationApplicationDto> RecordPaymentReferenceAsync(Guid id, RecordAdmissionCrmRegistrationPaymentReferenceDto input);
	Task<AdmissionCrmRegistrationApplicationDto> WaiveFeeAsync(Guid id, AdmissionCrmRegistrationReasonDto input);
}
