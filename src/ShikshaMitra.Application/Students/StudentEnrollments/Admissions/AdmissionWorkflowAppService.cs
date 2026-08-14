using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionWorkflowAppService(
	IRepository<AdmissionWorkflowDefinition, Guid> workflowRepository,
	IRepository<AdmissionWorkflowStageDefinition, Guid> stageRepository,
	IRepository<AdmissionWorkflowTransitionDefinition, Guid> transitionRepository) : ApplicationService, IAdmissionWorkflowAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public async Task<PagedResultDto<AdmissionWorkflowDto>> GetListAsync(AdmissionWorkflowListInput input)
	{
		var query = await workflowRepository.GetQueryableAsync();
		query = query.Where(x => (!input.Status.HasValue || x.Status == input.Status) &&
			(string.IsNullOrWhiteSpace(input.Search) || x.Name.Contains(input.Search) || x.Code.Contains(input.Search)));
		var count = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.CreationTime).Skip(input.SkipCount).Take(input.MaxResultCount));
		return new PagedResultDto<AdmissionWorkflowDto>(count, rows.Select(x => Map(x, [], [])).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public async Task<AdmissionWorkflowDto> GetAsync(Guid id)
	{
		var workflow = await GetWorkflowAsync(id);
		return await MapAsync(workflow);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Manage)]
	public async Task<AdmissionWorkflowDto> CreateAsync(CreateUpdateAdmissionWorkflowDto input)
	{
		await EnsureCodeAvailableAsync(input.Code, null);
		var workflow = new AdmissionWorkflowDefinition(GuidGenerator.Create(), CurrentTenant.Id, input.Name, input.Code);
		await workflowRepository.InsertAsync(workflow, autoSave: true);
		await ReplaceChildrenAsync(workflow.Id, input);
		return await MapAsync(workflow);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Manage)]
	public async Task<AdmissionWorkflowDto> UpdateAsync(Guid id, CreateUpdateAdmissionWorkflowDto input)
	{
		var workflow = await GetWorkflowAsync(id);
		await EnsureCodeAvailableAsync(input.Code, id);
		workflow.UpdateDetails(input.Name, input.Code);
		await ReplaceChildrenAsync(id, input);
		await workflowRepository.UpdateAsync(workflow, autoSave: true);
		return await MapAsync(workflow);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Manage)]
	public async Task<AdmissionWorkflowDto> CloneAsync(Guid id)
	{
		var source = await GetWorkflowAsync(id);
		var sourceStages = await GetStagesAsync(id);
		var sourceTransitions = await GetTransitionsAsync(id);
		var siblings = await AsyncExecuter.ToListAsync((await workflowRepository.GetQueryableAsync()).Where(x => x.Code == source.Code));
		var clone = new AdmissionWorkflowDefinition(GuidGenerator.Create(), CurrentTenant.Id, source.Name, source.Code, siblings.Max(x => x.Version) + 1, source.Id);
		await workflowRepository.InsertAsync(clone, autoSave: true);
		var stageMap = new Dictionary<Guid, Guid>();
		foreach (var stage in sourceStages)
		{
			var newId = GuidGenerator.Create();
			stageMap[stage.Id] = newId;
			await stageRepository.InsertAsync(new AdmissionWorkflowStageDefinition(newId, clone.Id, stage.Code, stage.Name, stage.StageType, stage.DisplayOrder, stage.IsRequired, stage.AllowManualSkip, stage.Description, stage.ConfigurationJson));
		}
		foreach (var transition in sourceTransitions)
			await transitionRepository.InsertAsync(new AdmissionWorkflowTransitionDefinition(GuidGenerator.Create(), clone.Id, stageMap[transition.FromStageId], stageMap[transition.ToStageId], transition.Priority));
		await CurrentUnitOfWork.SaveChangesAsync();
		return await MapAsync(clone);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public async Task<AdmissionWorkflowValidationResultDto> ValidateAsync(Guid id)
	{
		await GetWorkflowAsync(id);
		return await ValidateDefinitionAsync(id);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Publish)]
	public async Task<AdmissionWorkflowDto> PublishAsync(Guid id)
	{
		var workflow = await GetWorkflowAsync(id);
		var validation = await ValidateDefinitionAsync(id);
		if (!validation.IsValid) throw new BusinessException(AdmissionWorkflowErrorCodes.InvalidTransition).WithData("Errors", string.Join("; ", validation.Errors));
		workflow.Publish(CurrentUser.Id);
		await workflowRepository.UpdateAsync(workflow, autoSave: true);
		return await MapAsync(workflow);
	}

	private async Task<AdmissionWorkflowValidationResultDto> ValidateDefinitionAsync(Guid id)
	{
		var stages = await GetStagesAsync(id);
		var transitions = await GetTransitionsAsync(id);
		var result = new AdmissionWorkflowValidationResultDto();
		if (stages.Count == 0) { result.Errors.Add(AdmissionWorkflowErrorCodes.NoStages); return result; }
		if (stages.GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1)) result.Errors.Add(AdmissionWorkflowErrorCodes.DuplicateStageCode);
		var initial = stages.Where(x => x.DisplayOrder == stages.Min(s => s.DisplayOrder)).ToList();
		var final = stages.Where(x => x.DisplayOrder == stages.Max(s => s.DisplayOrder)).ToList();
		if (initial.Count != 1) result.Errors.Add(AdmissionWorkflowErrorCodes.InvalidInitialStage);
		if (final.Count != 1) result.Errors.Add(AdmissionWorkflowErrorCodes.InvalidFinalStage);
		var ids = stages.Select(x => x.Id).ToHashSet();
		if (transitions.Any(x => !ids.Contains(x.FromStageId) || !ids.Contains(x.ToStageId) || x.FromStageId == x.ToStageId)) result.Errors.Add(AdmissionWorkflowErrorCodes.InvalidTransition);
		var connected = transitions.SelectMany(x => new[] { x.FromStageId, x.ToStageId }).ToHashSet();
		if (stages.Count > 1 && stages.Any(x => !connected.Contains(x.Id))) result.Errors.Add(AdmissionWorkflowErrorCodes.OrphanStage);
		var initialId = initial.SingleOrDefault()?.Id;
		var finalId = final.SingleOrDefault()?.Id;
		if (initialId.HasValue && finalId.HasValue && stages.Count > 1)
		{
			if (!transitions.Any(x => x.FromStageId == initialId.Value)) result.Errors.Add(AdmissionWorkflowErrorCodes.InvalidTransition);
			if (!transitions.Any(x => x.ToStageId == finalId.Value)) result.Errors.Add(AdmissionWorkflowErrorCodes.InvalidTransition);
		}
		if (HasCycle(stages.Select(x => x.Id), transitions)) result.Errors.Add(AdmissionWorkflowErrorCodes.CycleDetected);
		return result;
	}

	private static bool HasCycle(IEnumerable<Guid> stageIds, IEnumerable<AdmissionWorkflowTransitionDefinition> transitions)
	{
		var edges = transitions.GroupBy(x => x.FromStageId).ToDictionary(x => x.Key, x => x.Select(y => y.ToStageId).ToList());
		var visiting = new HashSet<Guid>(); var visited = new HashSet<Guid>();
		bool Visit(Guid id) { if (visiting.Contains(id)) return true; if (!visited.Add(id)) return false; visiting.Add(id); if (edges.TryGetValue(id, out var next) && next.Any(Visit)) return true; visiting.Remove(id); return false; }
		return stageIds.Any(Visit);
	}

	private async Task ReplaceChildrenAsync(Guid workflowId, CreateUpdateAdmissionWorkflowDto input)
	{
		foreach (var stage in await GetStagesAsync(workflowId)) await stageRepository.DeleteAsync(stage, autoSave: false);
		foreach (var transition in await GetTransitionsAsync(workflowId)) await transitionRepository.DeleteAsync(transition, autoSave: false);
		var stageIds = new Dictionary<int, Guid>();
		var requestedStageIds = new Dictionary<Guid, Guid>();
		foreach (var item in input.Stages)
		{
			var id = item.Id.GetValueOrDefault(); if (id == Guid.Empty) id = GuidGenerator.Create();
			if (item.Id.HasValue) requestedStageIds[item.Id.Value] = id;
			stageIds[item.DisplayOrder] = id;
			await stageRepository.InsertAsync(new AdmissionWorkflowStageDefinition(id, workflowId, item.Code, item.Name, item.StageType, item.DisplayOrder, item.IsRequired, item.AllowManualSkip, item.Description, item.ConfigurationJson), autoSave: false);
		}
		foreach (var item in input.Transitions)
		{
			var fromId = requestedStageIds.GetValueOrDefault(item.FromStageId, item.FromStageId);
			var toId = requestedStageIds.GetValueOrDefault(item.ToStageId, item.ToStageId);
			await transitionRepository.InsertAsync(new AdmissionWorkflowTransitionDefinition(item.Id ?? GuidGenerator.Create(), workflowId, fromId, toId, item.Priority), autoSave: false);
		}
	}

	private async Task EnsureCodeAvailableAsync(string code, Guid? excludingId)
	{
		var normalized = code.Trim().ToUpperInvariant();
		var query = await workflowRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(query.Where(x => x.Code == normalized && (!excludingId.HasValue || x.Id != excludingId.Value) && x.Status != AdmissionWorkflowStatus.Archived))) throw new BusinessException(AdmissionWorkflowErrorCodes.CodeAlreadyExists);
	}
	private async Task<AdmissionWorkflowDefinition> GetWorkflowAsync(Guid id) => await workflowRepository.FindAsync(id) ?? throw new EntityNotFoundException(typeof(AdmissionWorkflowDefinition), id);
	private async Task<List<AdmissionWorkflowStageDefinition>> GetStagesAsync(Guid id) => await AsyncExecuter.ToListAsync((await stageRepository.GetQueryableAsync()).Where(x => x.WorkflowDefinitionId == id).OrderBy(x => x.DisplayOrder));
	private async Task<List<AdmissionWorkflowTransitionDefinition>> GetTransitionsAsync(Guid id) => await AsyncExecuter.ToListAsync((await transitionRepository.GetQueryableAsync()).Where(x => x.WorkflowDefinitionId == id).OrderBy(x => x.Priority));
	private async Task<AdmissionWorkflowDto> MapAsync(AdmissionWorkflowDefinition x) => Map(x, await GetStagesAsync(x.Id), await GetTransitionsAsync(x.Id));
	private static AdmissionWorkflowDto Map(AdmissionWorkflowDefinition x, List<AdmissionWorkflowStageDefinition> stages, List<AdmissionWorkflowTransitionDefinition> transitions) => new() { Id = x.Id, Name = x.Name, Code = x.Code, Version = x.Version, Status = x.Status, ParentVersionId = x.ParentVersionId, PublishedAt = x.PublishedAt, CreationTime = x.CreationTime, CreatorId = x.CreatorId, LastModificationTime = x.LastModificationTime, LastModifierId = x.LastModifierId, Stages = stages.Select(s => new AdmissionWorkflowStageDto { Id = s.Id, WorkflowDefinitionId = s.WorkflowDefinitionId, Code = s.Code, Name = s.Name, Description = s.Description, StageType = s.StageType, DisplayOrder = s.DisplayOrder, IsRequired = s.IsRequired, AllowManualSkip = s.AllowManualSkip, ConfigurationJson = s.ConfigurationJson }).ToList(), Transitions = transitions.Select(t => new AdmissionWorkflowTransitionDto { Id = t.Id, WorkflowDefinitionId = t.WorkflowDefinitionId, FromStageId = t.FromStageId, ToStageId = t.ToStageId, Priority = t.Priority }).ToList() };
}
