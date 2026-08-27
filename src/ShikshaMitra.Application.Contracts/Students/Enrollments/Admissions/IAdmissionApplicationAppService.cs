using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionApplicationAppService : IApplicationService
{
	Task<PagedResultDto<AdmissionApplicationDto>> GetListAsync(AdmissionApplicationListInput input);
	Task<AdmissionApplicationDto> GetAsync(Guid id);
	Task<AdmissionApplicationDto> StartAsync(Guid applicantId, Guid offeringId, Guid? inquiryId = null);
	Task<AdmissionApplicationWorkflowDto> GetWorkflowAsync(Guid id);
	Task<AdmissionApplicationFormDto> GetFormAsync(Guid id);
	Task<AdmissionApplicationFormDto> UpdateFormValuesAsync(Guid id, UpdateAdmissionApplicationFormValuesDto input);
	Task<AdmissionApplicationSubmitResultDto> SubmitAsync(Guid id);
	Task<AdmissionApplicationFormDto> ReopenFormAsync(Guid id);
	Task<List<AdmissionFormChangeHistoryDto>> GetFormHistoryAsync(Guid id);
	Task<AdmissionApplicationWorkflowDto> AdvanceWorkflowAsync(Guid id, AdvanceAdmissionWorkflowDto input);
	Task<AdmissionApplicationDto> MarkInReviewAsync(Guid id, AdmissionApplicationDecisionDto input);
	Task<AdmissionApplicationDto> ApproveAsync(Guid id, AdmissionApplicationDecisionDto input);
	Task<AdmissionApplicationDto> RejectAsync(Guid id, AdmissionApplicationDecisionDto input);
	Task<AdmissionApplicationDto> WaitlistAsync(Guid id, AdmissionApplicationDecisionDto input);
	Task<AdmissionApplicationDto> IssueOfferAsync(Guid id, AdmissionApplicationDecisionDto input);
	Task<AdmissionApplicationDto> AcceptOfferAsync(Guid id, AdmissionApplicationDecisionDto input);
	Task<AdmissionApplicationDto> DeclineOfferAsync(Guid id, AdmissionApplicationDecisionDto input);
	Task<AdmissionEnrollmentResultDto> EnrollAsync(Guid id, EnrollAdmissionApplicationDto input);
	Task<AdmissionEnrollmentOptionsDto> GetEnrollmentOptionsAsync(Guid id);
	Task<List<AdmissionApplicationDecisionHistoryDto>> GetDecisionHistoryAsync(Guid id);
}
