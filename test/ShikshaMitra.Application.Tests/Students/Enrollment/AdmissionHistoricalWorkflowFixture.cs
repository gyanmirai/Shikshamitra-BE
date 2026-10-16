using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	private Task<AdmissionWorkflowDto> SeedPublishedHistoricalWorkflowAsync(CreateUpdateAdmissionWorkflowDto input) =>
		WithUnitOfWorkAsync(async () =>
		{
			// Persist historical graphs for snapshot/read tests. Retired operations remain denied.
			var workflow = new AdmissionWorkflowDefinition(Guid.NewGuid(), TestTenantId,
				input.Name, input.Code, description: input.Description);
			workflow.Publish(TestUserId);
			await GetRequiredService<IRepository<AdmissionWorkflowDefinition, Guid>>().InsertAsync(workflow, false);
			var stages = GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>();
			foreach (var stage in input.Stages)
				await stages.InsertAsync(new AdmissionWorkflowStageDefinition(stage.Id!.Value, workflow.Id,
					stage.Code, stage.Name, stage.StageType, stage.DisplayOrder, stage.IsRequired,
					stage.AllowManualSkip, stage.Description, stage.ConfigurationJson,
					stage.IsEntryStage, stage.IsTerminalStage), false);
			var transitions = GetRequiredService<IRepository<AdmissionWorkflowTransitionDefinition, Guid>>();
			foreach (var transition in input.Transitions)
				await transitions.InsertAsync(new AdmissionWorkflowTransitionDefinition(Guid.NewGuid(), workflow.Id,
					transition.FromStageId, transition.ToStageId, transition.Priority,
					transition.ConditionRuleJson, transition.IsDefault), false);
			await GetRequiredService<Volo.Abp.Uow.IUnitOfWorkManager>().Current!.SaveChangesAsync();
			return await _workflowAppService.GetAsync(workflow.Id);
		});
}
