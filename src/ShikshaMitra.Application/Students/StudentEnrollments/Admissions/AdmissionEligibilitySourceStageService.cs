using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionEligibilitySourceStageService(IRepository<AdmissionWorkflowStageDefinition, Guid> definitions,
	IRepository<AdmissionWorkflowTransitionDefinition, Guid> transitions, IRepository<AdmissionApplicationStageInstance, Guid> stages) : ITransientDependency
{
	public async Task<AdmissionWorkflowStageDefinition?> FindDefinitionAsync(AdmissionWorkflowStageDefinition target, AdmissionEligibilityCriterion criterion)
	{
		var rows = await definitions.GetListAsync(item => item.WorkflowDefinitionId == target.WorkflowDefinitionId);
		var source = rows.SingleOrDefault(item => item.Code.Equals(criterion.SourceStageCode, StringComparison.OrdinalIgnoreCase));
		var type = criterion.Kind == "assessment" ? AdmissionWorkflowStageType.Assessment : AdmissionWorkflowStageType.DocumentVerification;
		if (source == null || source.Id == target.Id || source.StageType != type) return null;
		if (type == AdmissionWorkflowStageType.Assessment ? AdmissionAssessmentAccessService.ReadPolicy(source) == null : AdmissionDocumentAccessService.ReadPolicy(source) == null) return null;
		var links = await transitions.GetListAsync(item => item.WorkflowDefinitionId == target.WorkflowDefinitionId);
		var reachable = new HashSet<Guid>();
		var pending = new Queue<Guid>(); pending.Enqueue(source.Id);
		while (pending.TryDequeue(out var current))
		{
			if (!reachable.Add(current)) continue;
			foreach (var edge in links.Where(item => item.FromStageId == current)) pending.Enqueue(edge.ToStageId);
		}
		return reachable.Contains(target.Id) ? source : null;
	}
	public async Task<AdmissionApplicationStageInstance?> FindCompletedAsync(AdmissionEligibilityContext context, AdmissionEligibilityCriterion criterion)
	{
		var definition = await FindDefinitionAsync(context.Definition, criterion);
		if (definition == null) return null;
		return await stages.FindAsync(item => item.WorkflowInstanceId == context.Stage.WorkflowInstanceId && item.DefinitionStageId == definition.Id && item.Status == AdmissionRuntimeStageStatus.Completed);
	}
}
