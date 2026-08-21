using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionWorkflowReadService(
	IRepository<AdmissionWorkflowDefinition, Guid> workflowRepository,
	IRepository<AdmissionOffering, Guid> offeringRepository,
	IRepository<AdmissionCycle, Guid> cycleRepository,
	IRepository<AdmissionApplication, Guid> applicationRepository,
	AdmissionWorkflowDefinitionStore definitionStore)
	: ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<AdmissionWorkflowDto>> GetListAsync(AdmissionWorkflowListInput input)
	{
		var query = await workflowRepository.GetQueryableAsync();
		var offeringUsage = await offeringRepository.GetQueryableAsync();
		var applicationUsage = await applicationRepository.GetQueryableAsync();
		var search = input.Search?.Trim();
		query = query.Where(workflow =>
			(!input.Status.HasValue || workflow.Status == input.Status) &&
			(!input.Version.HasValue || workflow.Version == input.Version) &&
			(!input.CreatedFrom.HasValue || workflow.CreationTime >= input.CreatedFrom.Value) &&
			(!input.CreatedTo.HasValue || workflow.CreationTime <= input.CreatedTo.Value) &&
			(!input.IsUsed.HasValue || input.IsUsed.Value ==
				(offeringUsage.Any(offering => offering.WorkflowDefinitionId == workflow.Id) ||
				 applicationUsage.Any(application => application.WorkflowDefinitionId == workflow.Id))) &&
			(string.IsNullOrWhiteSpace(search) || workflow.Name.Contains(search) || workflow.Code.Contains(search)));

		var count = await AsyncExecuter.CountAsync(query);
		var workflows = await AsyncExecuter.ToListAsync(
			Sort(query, input.Sorting).Skip(input.SkipCount).Take(input.MaxResultCount));
		var ids = workflows.Select(workflow => workflow.Id).ToList();
		var offeringCounts = await GetOfferingCountsAsync(ids);
		var applicationCounts = await GetApplicationCountsAsync(ids);
		var items = workflows.Select(workflow =>
		{
			var dto = AdmissionWorkflowMapper.ToDto(workflow);
			dto.OfferingCount = offeringCounts.GetValueOrDefault(workflow.Id);
			dto.ApplicationCount = applicationCounts.GetValueOrDefault(workflow.Id);
			return dto;
		}).ToList();
		return new PagedResultDto<AdmissionWorkflowDto>(count, items);
	}

	public async Task<AdmissionWorkflowDto> GetAsync(Guid id) =>
		await definitionStore.GetDtoAsync(await definitionStore.GetAsync(id));

	public async Task<List<AdmissionWorkflowDto>> GetVersionHistoryAsync(Guid id)
	{
		var workflow = await definitionStore.GetAsync(id);
		var query = await workflowRepository.GetQueryableAsync();
		var versions = await AsyncExecuter.ToListAsync(query
			.Where(candidate => candidate.Code == workflow.Code)
			.OrderByDescending(candidate => candidate.Version));
		return versions.Select(workflowVersion => AdmissionWorkflowMapper.ToDto(workflowVersion)).ToList();
	}

	public async Task<AdmissionWorkflowUsageDto> GetUsageAsync(Guid id)
	{
		await definitionStore.GetAsync(id);
		var offeringQuery = await offeringRepository.GetQueryableAsync();
		var applicationQuery = await applicationRepository.GetQueryableAsync();
		var offerings = await AsyncExecuter.ToListAsync(offeringQuery.Where(offering => offering.WorkflowDefinitionId == id));
		var cycleIds = offerings.Select(offering => offering.AdmissionCycleId).Distinct().ToList();
		var cycles = await cycleRepository.GetListAsync(cycle => cycleIds.Contains(cycle.Id));
		var applicationCount = await AsyncExecuter.CountAsync(applicationQuery.Where(application => application.WorkflowDefinitionId == id));
		return new AdmissionWorkflowUsageDto
		{
			WorkflowDefinitionId = id,
			OfferingCount = offerings.Count,
			ApplicationCount = applicationCount,
			CanDelete = offerings.Count == 0 && applicationCount == 0,
			Offerings = offerings.OrderBy(offering => offering.Code).Select(offering => new AdmissionWorkflowOfferingUsageDto
			{
				Id = offering.Id,
				AdmissionCycleId = offering.AdmissionCycleId,
				AdmissionCycleName = cycles.FirstOrDefault(cycle => cycle.Id == offering.AdmissionCycleId)?.Name ?? string.Empty,
				Code = offering.Code,
				IsActive = offering.IsActive
			}).ToList()
		};
	}

	public IReadOnlyList<AdmissionWorkflowStageTypeMetadataDto> GetStageTypes() =>
		AdmissionWorkflowStageTypeRegistry.All.Select(metadata => new AdmissionWorkflowStageTypeMetadataDto
		{
			Type = metadata.Type,
			DisplayName = metadata.DisplayName,
			Description = metadata.Description,
			Icon = metadata.Icon,
			DefaultColor = metadata.DefaultColor,
			SupportedConfiguration = metadata.SupportedConfiguration,
			RuntimeHandler = metadata.RuntimeHandler,
			NextRequiredAction = metadata.NextRequiredAction,
			CanBeEntryStage = metadata.CanBeEntryStage,
			CanBeTerminalStage = metadata.CanBeTerminalStage,
			SupportsFormAttachment = metadata.SupportsFormAttachment,
			SupportsConditions = metadata.SupportsConditions,
			IsRuntimeReady = metadata.IsRuntimeReady
		}).ToList();

	private async Task<Dictionary<Guid, int>> GetOfferingCountsAsync(List<Guid> ids)
	{
		var query = await offeringRepository.GetQueryableAsync();
		var counts = await AsyncExecuter.ToListAsync(query
			.Where(offering => offering.WorkflowDefinitionId.HasValue && ids.Contains(offering.WorkflowDefinitionId.Value))
			.GroupBy(offering => offering.WorkflowDefinitionId!.Value)
			.Select(group => new { Id = group.Key, Count = group.Count() }));
		return counts.ToDictionary(item => item.Id, item => item.Count);
	}

	private async Task<Dictionary<Guid, int>> GetApplicationCountsAsync(List<Guid> ids)
	{
		var query = await applicationRepository.GetQueryableAsync();
		var counts = await AsyncExecuter.ToListAsync(query
			.Where(application => ids.Contains(application.WorkflowDefinitionId))
			.GroupBy(application => application.WorkflowDefinitionId)
			.Select(group => new { Id = group.Key, Count = group.Count() }));
		return counts.ToDictionary(item => item.Id, item => item.Count);
	}

	private static IQueryable<AdmissionWorkflowDefinition> Sort(
		IQueryable<AdmissionWorkflowDefinition> query,
		string? sorting) => sorting?.Trim().ToLowerInvariant() switch
	{
		"name" => query.OrderBy(workflow => workflow.Name),
		"name desc" => query.OrderByDescending(workflow => workflow.Name),
		"code" => query.OrderBy(workflow => workflow.Code).ThenByDescending(workflow => workflow.Version),
		"version" => query.OrderBy(workflow => workflow.Version),
		"version desc" => query.OrderByDescending(workflow => workflow.Version),
		"status" => query.OrderBy(workflow => workflow.Status),
		_ => query.OrderByDescending(workflow => workflow.LastModificationTime ?? workflow.CreationTime)
	};
}
