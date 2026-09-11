using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionReviewRuntimeService(AdmissionReviewAccessService access, AdmissionReviewStateService states,
	AdmissionReviewDecisionGuard decisions, IRepository<AdmissionReviewWorkItem, Guid> workItems,
	IRepository<AdmissionWorkflowStageDefinition, Guid> definitions, IRepository<AdmissionWorkflowTransitionDefinition, Guid> transitions,
	AdmissionWorkflowRuntimeValueService values, AdmissionDocumentRuntimeService documents, AdmissionEligibilityRuntimeService eligibility,
	AdmissionWorkflowTransitionSelector selector) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionReviewWorkItem> EnsureWorkItemAsync(AdmissionReviewContext context)
	{
		var existing = await workItems.FindAsync(item => item.StageInstanceId == context.Stage.Id);
		if (existing != null) return existing;
		var policy = context.Policy ?? throw new BusinessException(AdmissionReviewErrorCodes.InvalidPolicy);
		return await workItems.InsertAsync(new AdmissionReviewWorkItem(GuidGenerator.Create(), CurrentTenant.Id,
			context.Application.Id, context.Stage.Id, policy.AssignedReviewerId, policy.DueInDays is int days ? context.Stage.EnteredAt?.AddDays(days) : null), false);
	}
	public async Task EnterAsync(AdmissionApplication application, AdmissionApplicationStageInstance stage)
	{
		if (stage.StageType != AdmissionWorkflowStageType.Review || AdmissionReviewAccessService.ReadPolicy(await definitions.GetAsync(stage.DefinitionStageId)) == null) return;
		await EnsureWorkItemAsync(await access.GetContextAsync(application.Id, stage.Id));
	}
	public async Task<AdmissionReviewState> EnsureCompleteAsync(Guid applicationId, Guid stageId)
	{
		var state = await states.ReadAsync(await access.GetContextAsync(applicationId, stageId));
		state.EnsureSubmitted();
		if (!state.Context.IsActive || state.Context.Application.Status == AdmissionApplicationStatus.Draft ||
			state.Decision?.Decision is not ("approve" or "waitlist" or "reject")) throw new BusinessException(AdmissionReviewErrorCodes.ReviewRequired);
		if (state.Decision.Decision == "approve" && state.Evidence?.Passed != true) throw new BusinessException(AdmissionReviewErrorCodes.EvidenceRequired);
		return state;
	}
	public async Task<bool> CanCompleteAsync(AdmissionReviewState state)
	{
		try
		{
			await EnsureCompleteAsync(state.Context.Application.Id, state.Context.Stage.Id);
			var facts = await values.ReadAsync(state.Context.Application, protectManagedFacts: true);
			facts = await documents.AddFactsAsync(state.Context.Application, facts);
			facts = await eligibility.AddFactsAsync(state.Context.Application, facts);
			facts = await AddFactsAsync(state.Context.Application, facts, state);
			var links = await transitions.GetListAsync(item => item.WorkflowDefinitionId == state.Context.Application.WorkflowDefinitionId && item.FromStageId == state.Context.Definition.Id);
			var selected = state.Context.Definition.IsTerminalStage ? null : selector.Select(links, facts);
			AdmissionReviewRouteRules.EnsureRoute(state.Decision!.Decision, selected, facts); return true;
		}
		catch (BusinessException) { return false; }
	}
	public async Task<IReadOnlyDictionary<string, string>> AddFactsAsync(AdmissionApplication application, IReadOnlyDictionary<string, string> values, AdmissionReviewState? current = null)
	{
		current ??= await decisions.GetStateAsync(application);
		if (current == null) return values;
		var result = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
		foreach (var key in result.Keys.Where(key => key.StartsWith("review.", StringComparison.OrdinalIgnoreCase)).ToList()) result.Remove(key);
		result["review.status"] = current.Status;
		result["review.decision"] = current.IsStale ? "stale" : current.Decision?.Decision ?? "pending";
		result["application.status"] = application.Status.ToString(); return result;
	}
}
