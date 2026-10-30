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
	AdmissionInquiryManagementAppService inquiryManagement,
	AdmissionApplicationLifecycleService lifecycleService,
	AdmissionDirectRegistrationService directRegistrationService,
	AdmissionApplicationCounsellorService counsellorService,
	AdmissionApplicationWorkflowReadService workflowReadService,
	AdmissionApplicationWorkflowService workflowService,
	AdmissionApplicationFormService formService,
	AdmissionJourneyClosureService journeyClosure,
	AdmissionJourneyResumeService journeyResume,
	AdmissionApplicationDecisionService decisionService)
	: ApplicationService, IAdmissionApplicationAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<PagedResultDto<AdmissionApplicationDto>> GetListAsync(AdmissionApplicationListInput input) =>
		readService.GetListAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<PagedResultDto<AdmissionPipelineItemDto>> GetPipelineAsync(AdmissionPipelineListInput input) => pipelineService.GetListAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<AdmissionPipelineOptionsDto> GetPipelineOptionsAsync(AdmissionPipelineListInput input) => pipelineService.GetOptionsAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<AdmissionInquiryManagementDto> GetFollowUpsAsync(Guid id) => inquiryManagement.GetApplicationFollowUpsAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public Task<AdmissionInquiryManagementDto> ScheduleFollowUpAsync(Guid id, ScheduleAdmissionInquiryFollowUpDto input) => inquiryManagement.ScheduleApplicationFollowUpAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public Task<AdmissionInquiryManagementDto> CompleteFollowUpAsync(Guid id, Guid followUpId, CompleteAdmissionInquiryFollowUpDto input) => inquiryManagement.CompleteApplicationFollowUpAsync(id, followUpId, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public Task<AdmissionInquiryManagementDto> CancelFollowUpAsync(Guid id, Guid followUpId, CancelAdmissionInquiryFollowUpDto input) => inquiryManagement.CancelApplicationFollowUpAsync(id, followUpId, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<AdmissionApplicationDto> GetAsync(Guid id) => readService.GetAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<AdmissionApplicationDto?> FindAsync(Guid id) => readService.FindAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Start)]
	public Task<AdmissionApplicationDto> StartAsync(Guid applicantId, Guid offeringId, Guid? inquiryId = null) =>
		lifecycleService.StartAsync(applicantId, offeringId, inquiryId);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Start)]
	public async Task<AdmissionApplicationDto> StartDirectRegistrationAsync(StartAdmissionDirectRegistrationDto input)
	{
		using var unitOfWork = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true });
		var application = await directRegistrationService.StartAsync(input);
		await unitOfWork.CompleteAsync();
		return application;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Start)]
	public Task<List<AdmissionDirectFamilyMatchDto>> FindDirectRegistrationFamiliesAsync(
		Guid offeringId, string guardianMobile, string? guardianEmail = null) =>
		directRegistrationService.FindFamiliesAsync(offeringId, guardianMobile, guardianEmail);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<List<AdmissionInquiryCounsellorDto>> GetAssignableCounsellorsAsync(Guid? offeringId = null, string? filter = null) =>
		counsellorService.GetAssignableAsync(offeringId, filter);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Edit)]
	public Task<AdmissionApplicationDto> AssignCounsellorAsync(Guid id, AssignAdmissionApplicationCounsellorDto input) =>
		counsellorService.AssignAsync(id, input);

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

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Decide)]
	public Task<AdmissionApplicationDto> MarkInReviewAsync(Guid id, AdmissionApplicationDecisionDto input) => decisionService.MarkInReviewAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Decide)]
	public Task<AdmissionApplicationDto> ApproveAsync(Guid id, AdmissionApplicationDecisionDto input) => decisionService.ApproveAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Decide)]
	public Task<AdmissionApplicationDto> RejectAsync(Guid id, AdmissionApplicationDecisionDto input) => decisionService.RejectAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Decide)]
	public Task<AdmissionApplicationDto> WaitlistAsync(Guid id, AdmissionApplicationDecisionDto input) => decisionService.WaitlistAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Decide)]
	[UnitOfWork(true)]
	public Task<AdmissionApplicationDto> EndJourneyAsync(Guid id, EndAdmissionJourneyDto input) => journeyClosure.EndAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<AdmissionJourneyResumePreviewDto> GetResumePreviewAsync(Guid id) => journeyResume.PreviewAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Decide)]
	public Task<AdmissionApplicationDto> ResumeJourneyAsync(Guid id, ResumeAdmissionJourneyDto input) => journeyResume.ResumeAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Offer)]
	public Task<AdmissionApplicationDto> IssueOfferAsync(Guid id, AdmissionApplicationDecisionDto input) => decisionService.IssueOfferAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Offer)]
	public Task<AdmissionApplicationDto> AcceptOfferAsync(Guid id, AdmissionApplicationDecisionDto input) => decisionService.AcceptOfferAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Offer)]
	public Task<AdmissionApplicationDto> DeclineOfferAsync(Guid id, AdmissionApplicationDecisionDto input) => decisionService.DeclineOfferAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<AdmissionFinalizationPreviewDto> GetFinalizationPreviewAsync(Guid id) =>
		decisionService.GetFinalizationPreviewAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Finalize)]
	[UnitOfWork(true)]
	public Task<AdmissionFinalizationResultDto> FinalizeAsync(Guid id, FinalizeAdmissionApplicationDto input) =>
		decisionService.FinalizeAsync(id, input);

	[Obsolete("Admissions no longer creates operational enrollment. Use FinalizeAsync.")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Finalize)]
	public Task<AdmissionEnrollmentResultDto> EnrollAsync(Guid id, EnrollAdmissionApplicationDto input) => decisionService.EnrollAsync(id, input);

	[Obsolete("Placement is owned by Student Onboarding.")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Finalize)]
	public Task<AdmissionEnrollmentOptionsDto> GetEnrollmentOptionsAsync(Guid id) => decisionService.GetEnrollmentOptionsAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public Task<List<AdmissionApplicationDecisionHistoryDto>> GetDecisionHistoryAsync(Guid id) => decisionService.GetDecisionHistoryAsync(id);
}
