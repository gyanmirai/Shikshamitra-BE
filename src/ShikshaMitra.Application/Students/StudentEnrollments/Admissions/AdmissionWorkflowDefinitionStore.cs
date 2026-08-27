using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionWorkflowDefinitionStore(
	IRepository<AdmissionWorkflowDefinition, Guid> workflowRepository,
	IRepository<AdmissionWorkflowStageDefinition, Guid> stageRepository,
	IRepository<AdmissionWorkflowTransitionDefinition, Guid> transitionRepository)
	: ApplicationService, ITransientDependency
{
	public async Task<AdmissionWorkflowDefinition> GetAsync(Guid id) =>
		await workflowRepository.FindAsync(id) ??
		throw new EntityNotFoundException(typeof(AdmissionWorkflowDefinition), id);

	public async Task<List<AdmissionWorkflowStageDefinition>> GetStagesAsync(Guid workflowId)
	{
		var query = await stageRepository.GetQueryableAsync();
		return await AsyncExecuter.ToListAsync(query
			.Where(stage => stage.WorkflowDefinitionId == workflowId)
			.OrderBy(stage => stage.DisplayOrder));
	}

	public async Task<List<AdmissionWorkflowTransitionDefinition>> GetTransitionsAsync(Guid workflowId)
	{
		var query = await transitionRepository.GetQueryableAsync();
		return await AsyncExecuter.ToListAsync(query
			.Where(transition => transition.WorkflowDefinitionId == workflowId)
			.OrderBy(transition => transition.Priority));
	}

	public async Task<AdmissionWorkflowDto> GetDtoAsync(AdmissionWorkflowDefinition workflow) =>
		AdmissionWorkflowMapper.ToDto(
			workflow,
			await GetStagesAsync(workflow.Id),
			await GetTransitionsAsync(workflow.Id));

	public async Task ReplaceChildrenAsync(Guid workflowId, CreateUpdateAdmissionWorkflowDto input)
	{
		var existingStages = await GetStagesAsync(workflowId);
		var existingStagesById = existingStages.ToDictionary(stage => stage.Id);
		var stageIdMap = new Dictionary<Guid, Guid>();
		var stagesToDelete = existingStages
			.Where(stage => input.Stages.All(candidate => candidate.Id != stage.Id))
			.ToList();
		if (stagesToDelete.Count > 0)
		{
			var deletedStageIds = stagesToDelete.Select(stage => stage.Id).ToHashSet();
			foreach (var transition in (await GetTransitionsAsync(workflowId))
				.Where(transition => deletedStageIds.Contains(transition.FromStageId) || deletedStageIds.Contains(transition.ToStageId)))
				await transitionRepository.DeleteAsync(transition, autoSave: false);
			foreach (var stage in stagesToDelete)
				await stageRepository.DeleteAsync(stage, autoSave: false);
			await CurrentUnitOfWork!.SaveChangesAsync();
		}
		foreach (var inputStage in input.Stages.OrderBy(stage => stage.DisplayOrder))
		{
			var requestedId = inputStage.Id.GetValueOrDefault();
			var stageId = requestedId != Guid.Empty && existingStagesById.ContainsKey(requestedId)
				? requestedId : GuidGenerator.Create();
			if (requestedId != Guid.Empty) stageIdMap[requestedId] = stageId;
			if (existingStagesById.TryGetValue(stageId, out var existingStage))
			{
				existingStage.Update(
					inputStage.Code, inputStage.Name, inputStage.StageType, inputStage.DisplayOrder,
					inputStage.IsRequired, inputStage.AllowManualSkip, inputStage.Description,
					inputStage.ConfigurationJson, inputStage.IsEntryStage, inputStage.IsTerminalStage);
				await stageRepository.UpdateAsync(existingStage, autoSave: false);
			}
			else
			{
				await stageRepository.InsertAsync(CreateStage(workflowId, stageId, inputStage), autoSave: false);
			}
		}

		await ReplaceTransitionsAsync(workflowId, input.Transitions, stageIdMap);
		await CurrentUnitOfWork!.SaveChangesAsync();
	}

	public async Task CopyChildrenAsync(Guid sourceWorkflowId, Guid targetWorkflowId)
	{
		var sourceStages = await GetStagesAsync(sourceWorkflowId);
		var sourceTransitions = await GetTransitionsAsync(sourceWorkflowId);
		var stageIdMap = new Dictionary<Guid, Guid>();
		foreach (var sourceStage in sourceStages)
		{
			var stageId = GuidGenerator.Create();
			stageIdMap[sourceStage.Id] = stageId;
			await stageRepository.InsertAsync(new AdmissionWorkflowStageDefinition(
				stageId, targetWorkflowId, sourceStage.Code, sourceStage.Name, sourceStage.StageType,
				sourceStage.DisplayOrder, sourceStage.IsRequired, sourceStage.AllowManualSkip,
				sourceStage.Description, sourceStage.ConfigurationJson, sourceStage.IsEntryStage,
				sourceStage.IsTerminalStage), autoSave: false);
		}
		foreach (var sourceTransition in sourceTransitions)
		{
			await transitionRepository.InsertAsync(new AdmissionWorkflowTransitionDefinition(
				GuidGenerator.Create(), targetWorkflowId, stageIdMap[sourceTransition.FromStageId],
				stageIdMap[sourceTransition.ToStageId], sourceTransition.Priority,
				sourceTransition.ConditionRuleJson, sourceTransition.IsDefault), autoSave: false);
		}
		await CurrentUnitOfWork!.SaveChangesAsync();
	}

	public async Task DeleteChildrenAsync(Guid workflowId)
	{
		foreach (var transition in await GetTransitionsAsync(workflowId))
			await transitionRepository.DeleteAsync(transition, autoSave: false);
		foreach (var stage in await GetStagesAsync(workflowId))
			await stageRepository.DeleteAsync(stage, autoSave: false);
	}

	private async Task ReplaceTransitionsAsync(
		Guid workflowId,
		IEnumerable<CreateUpdateAdmissionWorkflowTransitionDto> inputTransitions,
		IReadOnlyDictionary<Guid, Guid> stageIdMap)
	{
		var existing = await GetTransitionsAsync(workflowId);
		var byId = existing.ToDictionary(transition => transition.Id);
		var retainedIds = new HashSet<Guid>();
		foreach (var input in inputTransitions)
		{
			var fromId = stageIdMap.GetValueOrDefault(input.FromStageId, input.FromStageId);
			var toId = stageIdMap.GetValueOrDefault(input.ToStageId, input.ToStageId);
			var requestedId = input.Id.GetValueOrDefault();
			if (requestedId != Guid.Empty && byId.TryGetValue(requestedId, out var transition))
			{
				transition.Update(fromId, toId, input.Priority, input.ConditionRuleJson, input.IsDefault);
				retainedIds.Add(transition.Id);
				await transitionRepository.UpdateAsync(transition, autoSave: false);
			}
			else
			{
				var added = new AdmissionWorkflowTransitionDefinition(
					GuidGenerator.Create(), workflowId, fromId, toId, input.Priority,
					input.ConditionRuleJson, input.IsDefault);
				retainedIds.Add(added.Id);
				await transitionRepository.InsertAsync(added, autoSave: false);
			}
		}
		foreach (var transition in existing.Where(transition => !retainedIds.Contains(transition.Id)))
			await transitionRepository.DeleteAsync(transition, autoSave: false);
	}

	private static AdmissionWorkflowStageDefinition CreateStage(
		Guid workflowId,
		Guid id,
		CreateUpdateAdmissionWorkflowStageDto input) => new(
			id, workflowId, input.Code, input.Name, input.StageType, input.DisplayOrder,
			input.IsRequired, input.AllowManualSkip, input.Description, input.ConfigurationJson,
			input.IsEntryStage, input.IsTerminalStage);
}
