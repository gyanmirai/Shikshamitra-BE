using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

/// <summary>
/// Repairs the named demo workflow created by the admissions E2E fixture.
/// This is intentionally limited to the draft demo record; user-authored workflows
/// must keep their explicit graph and should be repaired through the workflow builder.
/// </summary>
public sealed class AdmissionWorkflowDemoRepairSeederContributor(
	ICurrentTenant currentTenant,
	IGuidGenerator guidGenerator,
	IUnitOfWork unitOfWork,
	IRepository<AdmissionWorkflowDefinition, Guid> workflowRepository,
	IRepository<AdmissionWorkflowStageDefinition, Guid> stageRepository,
	IRepository<AdmissionWorkflowTransitionDefinition, Guid> transitionRepository)
	: BaseSeederContributor(currentTenant, guidGenerator, unitOfWork)
{
	private const string DemoWorkflowName = "Grade 11 Global Admission";

	public override async Task SeedAsync(DataSeedContext context)
	{
		var workflow = (await workflowRepository.GetListAsync(
			x => x.Status == AdmissionWorkflowStatus.Draft &&
				x.Name == DemoWorkflowName,
			includeDetails: false))
			.OrderByDescending(x => x.Version)
			.FirstOrDefault();

		if (workflow is null)
		{
			return;
		}

		var stages = (await stageRepository.GetListAsync(
			x => x.WorkflowDefinitionId == workflow.Id,
			includeDetails: false))
			.OrderBy(x => x.DisplayOrder)
			.ThenBy(x => x.Code)
			.ToList();

		if (stages.Count < 2)
		{
			return;
		}

		var existingTransitions = await transitionRepository.GetListAsync(
			x => x.WorkflowDefinitionId == workflow.Id,
			includeDetails: false);
		var existingEdges = existingTransitions
			.Select(x => (x.FromStageId, x.ToStageId))
			.ToHashSet();
		var stagesWithOutgoingTransitions = existingTransitions
			.Select(x => x.FromStageId)
			.ToHashSet();
		var missingTransitions = stages
			.Zip(stages.Skip(1), (from, to) => (from, to))
			.Where(edge => !stagesWithOutgoingTransitions.Contains(edge.from.Id))
			.Where(edge => existingEdges.Add((edge.from.Id, edge.to.Id)))
			.Select((edge, index) => new AdmissionWorkflowTransitionDefinition(
				GuidGenerator.Create(),
				workflow.Id,
				edge.from.Id,
				edge.to.Id,
				priority: index + 1,
				conditionRuleJson: null,
				isDefault: true))
			.ToList();

		if (missingTransitions.Count == 0)
		{
			return;
		}

		await transitionRepository.InsertManyAsync(missingTransitions, autoSave: false);
		await CurrentUnitOfWork.SaveChangesAsync();
	}
}
