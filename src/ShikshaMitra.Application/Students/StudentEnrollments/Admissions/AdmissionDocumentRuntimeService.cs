using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionDocumentRuntimeService(AdmissionDocumentAccessService access, AdmissionDocumentEvaluationService evaluator,
	IRepository<AdmissionApplicationWorkflowInstance, Guid> workflows, IRepository<AdmissionApplicationStageInstance, Guid> stages,
	IRepository<AdmissionWorkflowStageDefinition, Guid> definitions) : ITransientDependency
{
	public async Task<AdmissionDocumentEvaluation> EnsureCompleteAsync(Guid applicationId, Guid stageId)
	{
		var context = await access.GetContextAsync(applicationId, stageId);
		var evaluation = await evaluator.EvaluateAsync(context.Application, stageId, context.Policy!);
		evaluation.EnsureComplete();
		return evaluation;
	}

	public async Task EnsureApplicationCompleteAsync(AdmissionApplication application)
	{
		var workflow = await workflows.FindAsync(item => item.ApplicationId == application.Id);
		if (workflow == null) return;
		var visited = await stages.GetListAsync(item => item.WorkflowInstanceId == workflow.Id &&
			item.StageType == AdmissionWorkflowStageType.DocumentVerification && item.Status != AdmissionRuntimeStageStatus.Pending);
		foreach (var stage in visited)
		{
			var definition = await definitions.GetAsync(stage.DefinitionStageId);
			var policy = AdmissionDocumentAccessService.ReadPolicy(definition);
			if (policy == null) continue;
			(await evaluator.EvaluateAsync(application, stage.Id, policy)).EnsureComplete();
		}
	}

	public async Task<IReadOnlyDictionary<string, string>> AddFactsAsync(AdmissionApplication application,
		IReadOnlyDictionary<string, string> values, AdmissionDocumentEvaluation? current = null)
	{
		if (current == null)
		{
			var workflow = await workflows.FindAsync(item => item.ApplicationId == application.Id);
			if (workflow == null) return values;
			var candidates = await stages.GetListAsync(item => item.WorkflowInstanceId == workflow.Id &&
				item.StageType == AdmissionWorkflowStageType.DocumentVerification && item.Status != AdmissionRuntimeStageStatus.Pending);
			foreach (var stage in candidates.OrderByDescending(item => item.EnteredAt).ThenByDescending(item => item.DisplayOrder))
			{
				var policy = AdmissionDocumentAccessService.ReadPolicy(await definitions.GetAsync(stage.DefinitionStageId));
				if (policy == null) continue;
				current = await evaluator.EvaluateAsync(application, stage.Id, policy);
				break;
			}
		}
		if (current == null) return values;
		var result = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
		foreach (var key in result.Keys.Where(key => key.StartsWith("documents.", StringComparison.OrdinalIgnoreCase)).ToList()) result.Remove(key);
		foreach (var fact in current.Facts) result[fact.Key] = fact.Value;
		result["application.status"] = application.Status.ToString();
		return result;
	}
}
