using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionEligibilityRuntimeService(AdmissionEligibilityAccessService access, AdmissionEligibilityStateService states,
	IRepository<AdmissionApplicationWorkflowInstance, Guid> workflows, IRepository<AdmissionApplicationStageInstance, Guid> stages,
	IRepository<AdmissionWorkflowStageDefinition, Guid> definitions, IRepository<AdmissionWorkflowTransitionDefinition, Guid> transitions,
	AdmissionWorkflowRuntimeValueService runtimeValues, AdmissionDocumentRuntimeService documents, AdmissionWorkflowTransitionSelector selector) : ITransientDependency
{
	public async Task<AdmissionEligibilityState> EnsureEvidenceAsync(Guid applicationId, Guid stageId)
	{
		var context = await access.GetContextAsync(applicationId, stageId, true);
		if (!context.IsActive) throw new BusinessException(AdmissionEligibilityErrorCodes.InvalidState);
		var state = await states.ReadAsync(context); state.EnsureFresh(); return state;
	}
	public async Task EnsureApplicationCompleteAsync(AdmissionApplication application)
	{
		if (application.Status == AdmissionApplicationStatus.AdmissionFinalized) return;
		var workflow = await workflows.FindAsync(item => item.ApplicationId == application.Id);
		if (workflow == null) return;
		foreach (var stage in await stages.GetListAsync(item => item.WorkflowInstanceId == workflow.Id && item.StageType == AdmissionWorkflowStageType.Eligibility && item.Status != AdmissionRuntimeStageStatus.Pending))
		{
			var context = await access.GetContextAsync(application.Id, stage.Id);
			if (context.Policy == null) continue;
			var state = await states.ReadAsync(context); state.EnsureFresh();
			if (!state.Passed) throw new BusinessException(AdmissionEligibilityErrorCodes.EvaluationRequired);
		}
	}
	public async Task<bool> CanCompleteAsync(AdmissionEligibilityState state)
	{
		if (!state.Context.IsActive || !state.Context.CanMutate || state.Latest == null || state.IsStale) return false;
		try
		{
			var values = await runtimeValues.ReadAsync(state.Context.Application, protectManagedFacts: true);
			values = await documents.AddFactsAsync(state.Context.Application, values);
			values = await AddFactsAsync(state.Context.Application, values, state);
			var links = await transitions.GetListAsync(item => item.WorkflowDefinitionId == state.Context.Application.WorkflowDefinitionId && item.FromStageId == state.Context.Definition.Id);
			var selected = state.Context.Definition.IsTerminalStage ? null : selector.Select(links, values);
			AdmissionEligibilityEvidence.EnsureRoute(state.Status, selected, values);
			return true;
		}
		catch (BusinessException) { return false; }
	}
	public async Task<IReadOnlyDictionary<string, string>> AddFactsAsync(AdmissionApplication application, IReadOnlyDictionary<string, string> values, AdmissionEligibilityState? current = null)
	{
		if (current == null)
		{
			var workflow = await workflows.FindAsync(item => item.ApplicationId == application.Id);
			if (workflow == null) return values;
			var rows = await stages.GetListAsync(item => item.WorkflowInstanceId == workflow.Id && item.StageType == AdmissionWorkflowStageType.Eligibility && item.Status != AdmissionRuntimeStageStatus.Pending);
			foreach (var row in rows.OrderByDescending(item => item.EnteredAt).ThenByDescending(item => item.DisplayOrder))
			{
				if (AdmissionEligibilityAccessService.ReadPolicy(await definitions.GetAsync(row.DefinitionStageId)) == null) continue;
				current = await states.ReadAsync(await access.GetContextAsync(application.Id, row.Id)); break;
			}
		}
		if (current == null)
		{
			var configured = await definitions.GetListAsync(item => item.WorkflowDefinitionId == application.WorkflowDefinitionId && item.StageType == AdmissionWorkflowStageType.Eligibility);
			if (!configured.Any(item => AdmissionEligibilityAccessService.ReadPolicy(item) != null)) return values;
		}
		var result = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
		foreach (var key in result.Keys.Where(key => key.StartsWith("eligibility.", StringComparison.OrdinalIgnoreCase)).ToList()) result.Remove(key);
		if (current?.Latest != null) foreach (var fact in AdmissionEligibilityEvidence.Facts(current.Latest, current.Status, current.IsStale)) result[fact.Key] = fact.Value;
		else result["eligibility.status"] = "notChecked";
		result["application.status"] = application.Status.ToString();
		return result;
	}
}
