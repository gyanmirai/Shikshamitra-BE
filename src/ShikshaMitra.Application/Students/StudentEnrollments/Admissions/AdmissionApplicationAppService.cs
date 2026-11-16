using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionApplicationAppService(
	AdmissionApplicationReadService readService,
	AdmissionPipelineReadService pipelineService,
	AdmissionApplicationWorkflowReadService workflowReadService)
	: ApplicationService, IAdmissionApplicationAppService
{
	// Opening an application/workflow must not construct unrelated write graphs.
	// ABP's request-local lazy provider retains the existing service lifetimes and interceptors.
	private AdmissionInquiryManagementAppService InquiryManagement => LazyServiceProvider.LazyGetRequiredService<AdmissionInquiryManagementAppService>();
	private AdmissionApplicationLifecycleService LifecycleService => LazyServiceProvider.LazyGetRequiredService<AdmissionApplicationLifecycleService>();
	private AdmissionDirectRegistrationService DirectRegistrationService => LazyServiceProvider.LazyGetRequiredService<AdmissionDirectRegistrationService>();
	private AdmissionApplicationCounsellorService CounsellorService => LazyServiceProvider.LazyGetRequiredService<AdmissionApplicationCounsellorService>();
	private AdmissionApplicationWorkflowService WorkflowService => LazyServiceProvider.LazyGetRequiredService<AdmissionApplicationWorkflowService>();
	private AdmissionApplicationFormService FormService => LazyServiceProvider.LazyGetRequiredService<AdmissionApplicationFormService>();
	private AdmissionJourneyClosureService JourneyClosure => LazyServiceProvider.LazyGetRequiredService<AdmissionJourneyClosureService>();
	private AdmissionJourneyResumeService JourneyResume => LazyServiceProvider.LazyGetRequiredService<AdmissionJourneyResumeService>();
	private AdmissionApplicationDecisionService DecisionService => LazyServiceProvider.LazyGetRequiredService<AdmissionApplicationDecisionService>();

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<PagedResultDto<AdmissionApplicationDto>> GetListAsync(AdmissionApplicationListInput input) =>
		readService.GetListAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<PagedResultDto<AdmissionPipelineItemDto>> GetPipelineAsync(AdmissionPipelineListInput input) => pipelineService.GetListAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<AdmissionPipelineOptionsDto> GetPipelineOptionsAsync(AdmissionPipelineListInput input) => pipelineService.GetOptionsAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<AdmissionInquiryManagementDto> GetFollowUpsAsync(Guid id) => InquiryManagement.GetApplicationFollowUpsAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public Task<AdmissionInquiryManagementDto> ScheduleFollowUpAsync(Guid id, ScheduleAdmissionInquiryFollowUpDto input) => InquiryManagement.ScheduleApplicationFollowUpAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public Task<AdmissionInquiryManagementDto> CompleteFollowUpAsync(Guid id, Guid followUpId, CompleteAdmissionInquiryFollowUpDto input) => InquiryManagement.CompleteApplicationFollowUpAsync(id, followUpId, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public Task<AdmissionInquiryManagementDto> CancelFollowUpAsync(Guid id, Guid followUpId, CancelAdmissionInquiryFollowUpDto input) => InquiryManagement.CancelApplicationFollowUpAsync(id, followUpId, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<AdmissionApplicationDto> GetAsync(Guid id) => readService.GetAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<AdmissionApplicationDto?> FindAsync(Guid id) => readService.FindAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Start)]
	[UnitOfWork(true)]
	public Task<AdmissionApplicationDto> StartAsync(Guid applicantId, Guid offeringId, Guid? inquiryId = null) =>
		LifecycleService.StartAsync(applicantId, offeringId, inquiryId);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Start)]
	[UnitOfWork(true)]
	public async Task<AdmissionApplicationDto> StartDirectRegistrationAsync(StartAdmissionDirectRegistrationDto input)
	{
		using var unitOfWork = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true });
		var application = await DirectRegistrationService.StartAsync(input);
		await unitOfWork.CompleteAsync();
		return application;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Start)]
	public Task<List<AdmissionDirectFamilyMatchDto>> FindDirectRegistrationFamiliesAsync(
		Guid offeringId, string guardianMobile, string? guardianEmail = null) =>
		DirectRegistrationService.FindFamiliesAsync(offeringId, guardianMobile, guardianEmail);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<List<AdmissionInquiryCounsellorDto>> GetAssignableCounsellorsAsync(Guid? offeringId = null, string? filter = null) =>
		CounsellorService.GetAssignableAsync(offeringId, filter);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Edit)]
	public Task<AdmissionApplicationDto> AssignCounsellorAsync(Guid id, AssignAdmissionApplicationCounsellorDto input) =>
		CounsellorService.AssignAsync(id, input);

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
		return await WorkflowService.AdvanceAsync(id, input);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<AdmissionApplicationFormDto> GetFormAsync(Guid id) => FormService.GetAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Edit)]
	public Task<AdmissionApplicationFormDto> UpdateFormValuesAsync(
		Guid id,
		UpdateAdmissionApplicationFormValuesDto input) => FormService.UpdateAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Edit)]
	public Task<AdmissionApplicationFormDto> CorrectFormAsync(Guid id, CorrectAdmissionApplicationFormDto input) =>
		FormService.CorrectAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Submit)]
	public Task<AdmissionApplicationSubmitResultDto> SubmitAsync(Guid id) => FormService.SubmitAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Reopen)]
	public Task<AdmissionApplicationFormDto> ReopenFormAsync(Guid id) => FormService.ReopenAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<List<AdmissionFormChangeHistoryDto>> GetFormHistoryAsync(Guid id) => FormService.GetHistoryAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Decide)]
	public Task<AdmissionApplicationDto> MarkInReviewAsync(Guid id, AdmissionApplicationDecisionDto input) => DecisionService.MarkInReviewAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Decide)]
	public Task<AdmissionApplicationDto> ApproveAsync(Guid id, AdmissionApplicationDecisionDto input) => DecisionService.ApproveAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Decide)]
	public Task<AdmissionApplicationDto> RejectAsync(Guid id, AdmissionApplicationDecisionDto input) => DecisionService.RejectAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Decide)]
	public Task<AdmissionApplicationDto> WaitlistAsync(Guid id, AdmissionApplicationDecisionDto input) => DecisionService.WaitlistAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Decide)]
	[UnitOfWork(true)]
	public Task<AdmissionApplicationDto> EndJourneyAsync(Guid id, EndAdmissionJourneyDto input) => JourneyClosure.EndAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<AdmissionJourneyResumePreviewDto> GetResumePreviewAsync(Guid id) => JourneyResume.PreviewAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Decide)]
	public Task<AdmissionApplicationDto> ResumeJourneyAsync(Guid id, ResumeAdmissionJourneyDto input) => JourneyResume.ResumeAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Offer)]
	public Task<AdmissionApplicationDto> IssueOfferAsync(Guid id, AdmissionApplicationDecisionDto input) => DecisionService.IssueOfferAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Offer)]
	public Task<AdmissionApplicationDto> AcceptOfferAsync(Guid id, AdmissionApplicationDecisionDto input) => DecisionService.AcceptOfferAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Offer)]
	public Task<AdmissionApplicationDto> DeclineOfferAsync(Guid id, AdmissionApplicationDecisionDto input) => DecisionService.DeclineOfferAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<AdmissionFinalizationPreviewDto> GetFinalizationPreviewAsync(Guid id) =>
		DecisionService.GetFinalizationPreviewAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Finalize)]
	[UnitOfWork(true)]
	public Task<AdmissionFinalizationResultDto> FinalizeAsync(Guid id, FinalizeAdmissionApplicationDto input) =>
		DecisionService.FinalizeAsync(id, input);

	[Obsolete("Admissions no longer creates operational enrollment. Use FinalizeAsync.")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Finalize)]
	public Task<AdmissionEnrollmentResultDto> EnrollAsync(Guid id, EnrollAdmissionApplicationDto input) => DecisionService.EnrollAsync(id, input);

	[Obsolete("Placement is owned by Student Onboarding.")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Finalize)]
	public Task<AdmissionEnrollmentOptionsDto> GetEnrollmentOptionsAsync(Guid id) => DecisionService.GetEnrollmentOptionsAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<List<AdmissionApplicationDecisionHistoryDto>> GetDecisionHistoryAsync(Guid id) => DecisionService.GetDecisionHistoryAsync(id);
}
