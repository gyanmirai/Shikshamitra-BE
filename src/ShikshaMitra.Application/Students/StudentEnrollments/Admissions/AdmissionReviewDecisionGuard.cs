using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionReviewDecisionGuard(AdmissionReviewAccessService access, AdmissionReviewStateService states,
	AdmissionReviewPeopleService people, IRepository<AdmissionWorkflowStageDefinition, Guid> definitions,
	IRepository<AdmissionApplicationWorkflowInstance, Guid> workflows, IRepository<AdmissionApplicationStageInstance, Guid> stages,
	IRepository<AdmissionReviewDecision, Guid> decisions, IRepository<AdmissionReviewWorkItem, Guid> workItems) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionReviewState?> GetStateAsync(AdmissionApplication application)
	{
		var configured = await definitions.GetListAsync(item => item.WorkflowDefinitionId == application.WorkflowDefinitionId && item.StageType == AdmissionWorkflowStageType.Review);
		var managed = configured.Where(item => AdmissionReviewAccessService.ReadPolicy(item) != null).ToList();
		if (managed.Count == 0) return null;
		if (managed.Count != 1) throw new BusinessException(AdmissionReviewErrorCodes.InvalidPolicy);
		var workflow = await workflows.FindAsync(item => item.ApplicationId == application.Id);
		if (workflow == null) throw new BusinessException(AdmissionReviewErrorCodes.ReviewRequired);
		var definitionId = managed[0].Id;
		var stage = await stages.FindAsync(item => item.WorkflowInstanceId == workflow.Id && item.DefinitionStageId == definitionId);
		if (stage == null) throw new BusinessException(AdmissionReviewErrorCodes.ReviewRequired);
		return await states.ReadAsync(await access.GetContextAsync(application.Id, stage.Id));
	}
	public async Task<AdmissionReviewState?> EnsureDecisionAsync(AdmissionApplication application, string decision)
	{
		var state = await GetStateAsync(application); if (state == null) return null;
		state.EnsureSubmitted();
		if (state.Decision != null || !await people.CanDecideAsync(state)) throw new BusinessException(AdmissionReviewErrorCodes.DecisionNotAllowed);
		if (decision == "approve" && state.Evidence?.Passed != true) throw new BusinessException(AdmissionReviewErrorCodes.EvidenceRequired);
		return state;
	}
	public async Task RecordDecisionAsync(AdmissionReviewState? state, string decision, string? reason)
	{
		if (state?.Latest == null) return;
		await decisions.InsertAsync(new AdmissionReviewDecision(GuidGenerator.Create(), CurrentTenant.Id, state.Context.Application.Id,
			state.Context.Stage.Id, state.Latest.Id, decision, reason, CurrentUser.Id!.Value, DateTime.UtcNow), false);
		if (state.WorkItem != null)
		{
			state.WorkItem.Record(decision switch { "approve" => "approved", "waitlist" => "waitlisted", "reject" => "rejected", _ => "correctionRequested" });
			await workItems.UpdateAsync(state.WorkItem, false);
		}
	}
	public async Task EnsureApplicationCompleteAsync(AdmissionApplication application)
	{
		if (application.Status == AdmissionApplicationStatus.AdmissionFinalized) return;
		var state = await GetStateAsync(application); if (state == null) return;
		state.EnsureSubmitted();
		if (!IsSatisfied(state)) throw new BusinessException(AdmissionReviewErrorCodes.ReviewRequired);
	}
	public static bool IsSatisfied(AdmissionReviewState state) => state.Latest?.Status == "submitted" && !state.IsStale &&
		state.Evidence?.Passed == true && state.Decision?.Decision == "approve";
	public async Task<AdmissionReviewDecisionCapabilitiesDto?> GetCapabilitiesAsync(AdmissionApplication application)
	{
		var state = await GetStateAsync(application); return state == null ? null : await GetCapabilitiesAsync(state);
	}
	public async Task<AdmissionReviewDecisionCapabilitiesDto> GetCapabilitiesAsync(AdmissionReviewState state)
	{
		var result = new AdmissionReviewDecisionCapabilitiesDto { ManagedReviewEnabled = state.Context.Policy != null, IsSatisfied = IsSatisfied(state) };
		if (!result.ManagedReviewEnabled) return result;
		try { state.EnsureSubmitted(); }
		catch (BusinessException exception) { result.Blockers.Add(Message(exception.Code)); return result; }
		if (state.Decision != null)
		{
			if (!result.IsSatisfied) result.Blockers.Add("A new review is required before changing this admission decision.");
			return result;
		}
		if (!await people.CanDecideAsync(state))
		{
			result.Blockers.Add(state.Context.Policy?.RequireIndependentApprover == true && state.Latest?.RecordedBy == CurrentUser.Id
				? "A different authorized approver must decide this review." : "An authorized approver in the configured role must decide this review.");
			return result;
		}
		var status = state.Context.Application.Status;
		result.CanApprove = state.Evidence?.Passed == true && status is AdmissionApplicationStatus.Submitted or AdmissionApplicationStatus.InReview or
			AdmissionApplicationStatus.Waitlisted or AdmissionApplicationStatus.Approved or AdmissionApplicationStatus.Offered or AdmissionApplicationStatus.OfferAccepted;
		result.CanReject = status is AdmissionApplicationStatus.Submitted or AdmissionApplicationStatus.InReview or AdmissionApplicationStatus.Waitlisted or AdmissionApplicationStatus.Approved;
		result.CanWaitlist = status is AdmissionApplicationStatus.Submitted or AdmissionApplicationStatus.InReview;
		if (state.Evidence?.Passed != true) result.Blockers.Add("Resolve the selected prerequisite results before approving admission.");
		return result;
	}
	public static string Message(string? code) => code switch
	{
		AdmissionReviewErrorCodes.StaleReview => "The reviewed information changed. Submit a fresh review before making the decision.",
		AdmissionReviewErrorCodes.EvidenceRequired => "Complete or refresh the selected prerequisite evidence before deciding this review.",
		_ => "Complete the review checklist and submit a recommendation before making the admission decision."
	};
}
