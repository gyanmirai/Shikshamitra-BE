using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionApplicationAppService : IApplicationService
{
	Task<PagedResultDto<AdmissionApplicationDto>> GetListAsync(AdmissionApplicationListInput input);
	Task<PagedResultDto<AdmissionPipelineItemDto>> GetPipelineAsync(AdmissionPipelineListInput input);
	Task<AdmissionPipelineOptionsDto> GetPipelineOptionsAsync(AdmissionPipelineListInput input);
	Task<AdmissionInquiryManagementDto> GetFollowUpsAsync(Guid id);
	Task<AdmissionInquiryManagementDto> ScheduleFollowUpAsync(Guid id, ScheduleAdmissionInquiryFollowUpDto input);
	Task<AdmissionInquiryManagementDto> CompleteFollowUpAsync(Guid id, Guid followUpId, CompleteAdmissionInquiryFollowUpDto input);
	Task<AdmissionInquiryManagementDto> CancelFollowUpAsync(Guid id, Guid followUpId, CancelAdmissionInquiryFollowUpDto input);
	Task<AdmissionApplicationDto> GetAsync(Guid id);
	Task<AdmissionApplicationDto?> FindAsync(Guid id);
	Task<AdmissionApplicationDto> StartAsync(Guid applicantId, Guid offeringId, Guid? inquiryId = null);
	Task<AdmissionApplicationDto> StartDirectRegistrationAsync(StartAdmissionDirectRegistrationDto input);
	Task<List<AdmissionDirectFamilyMatchDto>> FindDirectRegistrationFamiliesAsync(Guid offeringId, string guardianMobile);
	Task<List<AdmissionInquiryCounsellorDto>> GetAssignableCounsellorsAsync(Guid? offeringId = null, string? filter = null);
	Task<AdmissionApplicationDto> AssignCounsellorAsync(Guid id, AssignAdmissionApplicationCounsellorDto input);
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
	Task<AdmissionFinalizationPreviewDto> GetFinalizationPreviewAsync(Guid id);
	Task<AdmissionFinalizationResultDto> FinalizeAsync(Guid id, FinalizeAdmissionApplicationDto input);
	[Obsolete("Admissions no longer creates operational enrollment. Use FinalizeAsync and Student Onboarding.")]
	Task<AdmissionEnrollmentResultDto> EnrollAsync(Guid id, EnrollAdmissionApplicationDto input);
	[Obsolete("Placement is owned by Student Onboarding.")]
	Task<AdmissionEnrollmentOptionsDto> GetEnrollmentOptionsAsync(Guid id);
	Task<List<AdmissionApplicationDecisionHistoryDto>> GetDecisionHistoryAsync(Guid id);
}
