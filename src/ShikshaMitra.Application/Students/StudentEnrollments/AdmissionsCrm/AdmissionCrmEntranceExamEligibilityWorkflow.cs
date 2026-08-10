using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmEntranceExamEligibilityWorkflow(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	IRepository<AdmissionCrmEntranceExamRecord, Guid> examRepository,
	AdmissionCrmEntranceExamAccess access,
	AdmissionCrmEntranceExamTimeline timeline,
	AdmissionCrmEntranceExamJourney journey) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmEntranceExamDto> DecideAsync(
		Guid examRecordId,
		UpdateAdmissionCrmEligibilityDecisionDto input)
	{
		var exam = await access.GetExamAsync(examRecordId, "DecideEligibilityAsync");
		var lead = await access.GetLeadAsync(exam.LeadId, "DecideEligibilityAsync");
		var previousStage = lead.Stage;
		if (input.OverrideDecision && !await AuthorizationService.IsGrantedAsync(
			    ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.OverrideEligibility))
			throw new AbpAuthorizationException("Eligibility override permission is required.");

		exam.SetEligibilityDecision(input.Decision, input.Reason, input.OverrideDecision,
			input.OverrideReason, input.MeritRank, Clock.Now);
		AdmissionCrmEntranceExamMapper.ApplyLeadStage(lead, exam);
		await examRepository.UpdateAsync(exam, autoSave: false);
		await timeline.AddAsync(lead, AdmissionCrmTimelineEventType.EligibilityDecided,
			"Eligibility decision updated", $"{input.Decision}: {input.Reason}", exam.Id);
		await leadRepository.UpdateAsync(lead, autoSave: true);
		await journey.RunAsync(lead, AdmissionCrmJourneyTrigger.EligibilityDecided);
		await journey.RunStageReachedIfChangedAsync(lead, previousStage);
		return AdmissionCrmEntranceExamMapper.ToDto(exam, lead);
	}
}
