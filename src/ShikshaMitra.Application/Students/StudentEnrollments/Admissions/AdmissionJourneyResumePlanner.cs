using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionJourneyResumePlan(AdmissionJourneyResumePreviewDto Preview,
	AdmissionJourneyClosureSnapshot? Snapshot, AdmissionApplicationWorkflowInstance? Workflow,
	List<AdmissionApplicationStageInstance> Stages);

[RemoteService(false)]
public class AdmissionJourneyResumePlanner(
	IRepository<AdmissionApplicationWorkflowInstance, Guid> workflows,
	IRepository<AdmissionApplicationStageInstance, Guid> stages,
	IRepository<AdmissionApplicationDecisionHistory, Guid> history,
	IRepository<AdmissionWorkflowDefinition, Guid> definitions,
	IRepository<AdmissionFormInstance, Guid> forms,
	IRepository<AcademicYear, Guid> years,
	IRepository<AdmissionCycle, Guid> cycles,
	IRepository<StudentRecordReturnHandoff, Guid> returns,
	AdmissionOfferingRuntimeService offerings,
	AdmissionApplicationReadService reader) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionJourneyResumePlan> BuildAsync(AdmissionApplication application)
	{
		var preview = new AdmissionJourneyResumePreviewDto { ConcurrencyStamp = application.ConcurrencyStamp };
		var workflow = await workflows.FindAsync(x => x.ApplicationId == application.Id);
		var runtime = workflow == null ? [] : await stages.GetListAsync(x => x.WorkflowInstanceId == workflow.Id);
		var decisions = await history.GetListAsync(x => x.ApplicationId == application.Id);
		var closure = decisions.Where(x => x.Action == "journey-ended").OrderByDescending(x => x.OccurredAt).FirstOrDefault();
		preview.ClosureHistoryId = closure?.Id;
		if (application.Status != AdmissionApplicationStatus.Withdrawn || application.StudentId.HasValue ||
			application.OnboardingCaseId.HasValue || application.StudentEnrollmentId.HasValue || application.EnrolledStudentId.HasValue)
			preview.Blockers.Add("Only an admission ended before finalization can be resumed here.");
		if (await returns.FindAsync(x => x.AdmissionApplicationId == application.Id && x.TransitionId.HasValue) != null)
			preview.Blockers.Add("This admission belongs to a cancelled Student Records return. Recover the return through Student Records.");
		AdmissionJourneyClosureSnapshot? snapshot = null;
		if (closure != null && workflow != null)
		{
			try { snapshot = string.IsNullOrWhiteSpace(closure.MetadataJson) ? Legacy(application, workflow, runtime, decisions, closure,
				await forms.FindAsync(x => x.ApplicationId == application.Id)) : JsonSerializer.Deserialize<AdmissionJourneyClosureSnapshot>(closure.MetadataJson); }
			catch (JsonException) { /* Corrupt legacy evidence is a blocker, never a guessed restart. */ }
		}
		if (snapshot == null || workflow == null || closure == null || !Consistent(application, workflow, runtime, snapshot, closure))
			preview.Blockers.Add("The original pending stage could not be recovered safely. An administrator must review the withdrawal history.");
		else
		{
			preview.TargetStageId = snapshot.CurrentStageId;
			preview.TargetStageName = snapshot.CurrentStageId.HasValue ? runtime.Single(x => x.Id == snapshot.CurrentStageId).Name : "Admission finalization";
			if (snapshot.PreviousStatus is AdmissionApplicationStatus.Offered or AdmissionApplicationStatus.OfferAccepted &&
				(!snapshot.OfferExpiresAt.HasValue || snapshot.OfferExpiresAt.Value <= Clock.Now))
				preview.Blockers.Add("The previous offer has expired. An authorized offer review is required before resuming.");
			var pinned = await definitions.FindAsync(application.WorkflowDefinitionId);
			if (pinned == null || !pinned.Status.IsRuntimeVersion()) preview.Blockers.Add("The application's saved workflow version is unavailable.");
		}
		var cycle = await cycles.FindAsync(application.AdmissionCycleId);
		var year = cycle == null ? null : await years.FindAsync(cycle.AcademicYearId);
		if (cycle == null || year == null) preview.Blockers.Add("The original admission period or academic year is unavailable. Ask an administrator to review its configuration.");
		else if (AdmissionCycleWindowEvaluator.GetInstitutionDate(Clock.Now) > year.EndDate.Date)
			preview.Blockers.Add("The original academic year has ended. Start a new admission for the applicable year.");
		if (cycle?.Status is AdmissionCycleStatus.Closed or AdmissionCycleStatus.Archived)
			preview.Blockers.Add("The original admission period is closed. An authorized staff member must reopen the intake before this admission can resume.");
		else
		{
			try
			{
				var context = await offerings.GetAvailableAsync(application.AdmissionOfferingId);
				if (context.Cycle.Id != application.AdmissionCycleId) preview.Blockers.Add("The offering no longer matches the original admission period.");
			}
			catch (BusinessException error) { preview.Blockers.Add(error.Message); }
		}
		var details = await reader.GetAsync(application.Id);
		preview.TargetWorkspace = details.HasReachedDecision == true ? "Applications" : "Pipeline";
		preview.Warnings.Add("Released assessment bookings and cancelled follow-ups stay cancelled. Existing evidence and payments are retained.");
		preview.CanResume = preview.Blockers.Count == 0;
		return new(preview, snapshot, workflow, runtime);
	}

	private static bool Consistent(AdmissionApplication application, AdmissionApplicationWorkflowInstance workflow,
		List<AdmissionApplicationStageInstance> stages, AdmissionJourneyClosureSnapshot saved, AdmissionApplicationDecisionHistory closure)
	{
		if (saved.Version != 1 || saved.WorkflowId != workflow.Id || saved.WorkflowDefinitionId != application.WorkflowDefinitionId ||
			saved.OfferingId != application.AdmissionOfferingId || saved.CycleId != application.AdmissionCycleId ||
			workflow.Status != AdmissionWorkflowInstanceStatus.Cancelled || saved.PreviousStatus is not
			(AdmissionApplicationStatus.Draft or AdmissionApplicationStatus.Submitted or AdmissionApplicationStatus.InReview or AdmissionApplicationStatus.Waitlisted or
			 AdmissionApplicationStatus.Approved or AdmissionApplicationStatus.Offered or AdmissionApplicationStatus.OfferAccepted)) return false;
		if (saved.WorkflowStatus == AdmissionWorkflowInstanceStatus.Active && (!saved.CurrentStageId.HasValue ||
			!stages.Any(x => x.Id == saved.CurrentStageId && x.EnteredAt.HasValue))) return false;
		if (saved.WorkflowStatus == AdmissionWorkflowInstanceStatus.Completed &&
			(saved.CurrentStageId.HasValue || !saved.WorkflowCompletedAt.HasValue)) return false;
		if (saved.WorkflowStatus is not (AdmissionWorkflowInstanceStatus.Active or AdmissionWorkflowInstanceStatus.Completed)) return false;
		var cancelled = stages.Where(x => x.Status == AdmissionRuntimeStageStatus.Cancelled).ToArray();
		var expected = saved.PendingStageIds.Concat(saved.CurrentStageId.HasValue ? new[] { saved.CurrentStageId.Value } : []).ToArray();
		return application.DecisionAt.HasValue && expected.Distinct().Count() == expected.Length && cancelled.Select(x => x.Id).Order().SequenceEqual(expected.Order()) &&
			cancelled.All(x => x.CompletionReason == closure.Reason && x.CompletedBy == closure.ActorId && x.CompletedAt.HasValue &&
				x.CompletedAt.Value >= application.DecisionAt.Value && x.CompletedAt.Value <= closure.OccurredAt);
	}

	private static AdmissionJourneyClosureSnapshot? Legacy(AdmissionApplication application, AdmissionApplicationWorkflowInstance workflow,
		List<AdmissionApplicationStageInstance> stages, List<AdmissionApplicationDecisionHistory> decisions,
		AdmissionApplicationDecisionHistory closure, AdmissionFormInstance? form)
	{
		// Reconstruct only an unambiguous pre-decision closure. Old offered/reopened/branched records require review.
		var target = stages.SingleOrDefault(x => x.Id == closure.StageInstanceId);
		if (form == null || target == null || !target.EnteredAt.HasValue || stages.Any(x => x.StageType is AdmissionWorkflowStageType.Approval or
			AdmissionWorkflowStageType.Offer or AdmissionWorkflowStageType.Seat or AdmissionWorkflowStageType.Enrollment && x.EnteredAt.HasValue) ||
			decisions.Any(x => x.Id != closure.Id && x.Action != "journey-ended" && x.Action != "journey-resumed") ||
			stages.Any(x => x.Status == AdmissionRuntimeStageStatus.Cancelled && x.Id != target.Id && x.EnteredAt.HasValue)) return null;
		var status = form.Status == AdmissionFormInstanceStatus.Submitted ? AdmissionApplicationStatus.Submitted : AdmissionApplicationStatus.Draft;
		return AdmissionJourneyClosureSnapshot.Capture(application, workflow, []) with
		{
			PreviousStatus = status, DecisionReason = null, DecisionBy = null, DecisionAt = null,
			CurrentStageId = target.Id, WorkflowStatus = AdmissionWorkflowInstanceStatus.Active, WorkflowCompletedAt = null,
			PendingStageIds = stages.Where(x => x.Status == AdmissionRuntimeStageStatus.Cancelled && x.Id != target.Id).Select(x => x.Id).ToArray()
		};
	}
}
