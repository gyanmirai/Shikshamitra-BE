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
	IRepository<AdmissionApplicationWorkflowInstance, Guid> workflowInstanceRepository,
	IRepository<AdmissionApplicationStageInstance, Guid> stageInstanceRepository,
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
		var campusCounts = await GetCampusCountsAsync(ids);
		var applicationCounts = await GetApplicationCountsAsync(ids);
		var items = workflows.Select(workflow =>
		{
			var dto = AdmissionWorkflowMapper.ToDto(workflow);
			dto.OfferingCount = offeringCounts.GetValueOrDefault(workflow.Id);
			dto.CampusCount = campusCounts.GetValueOrDefault(workflow.Id);
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
		var ids = versions.Select(version => version.Id).ToList();
		var offeringCounts = await GetOfferingCountsAsync(ids);
		var campusCounts = await GetCampusCountsAsync(ids);
		var applicationCounts = await GetApplicationCountsAsync(ids);
		var result = new List<AdmissionWorkflowDto>(versions.Count);
		foreach (var version in versions)
		{
			var dto = await definitionStore.GetDtoAsync(version);
			dto.OfferingCount = offeringCounts.GetValueOrDefault(version.Id);
			dto.CampusCount = campusCounts.GetValueOrDefault(version.Id);
			dto.ApplicationCount = applicationCounts.GetValueOrDefault(version.Id);
			result.Add(dto);
		}
		return result;
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
			CampusCount = offerings.Where(offering => offering.CampusId.HasValue)
				.Select(offering => offering.CampusId!.Value).Distinct().Count(),
			ApplicationCount = applicationCount,
			CanDelete = offerings.Count == 0 && applicationCount == 0,
			RuntimeAnalytics = await GetRuntimeAnalyticsAsync(id),
			Offerings = offerings.OrderBy(offering => offering.Code).Select(offering => new AdmissionWorkflowOfferingUsageDto
			{
				Id = offering.Id,
				AdmissionCycleId = offering.AdmissionCycleId,
				CampusId = offering.CampusId,
				AdmissionCycleName = cycles.FirstOrDefault(cycle => cycle.Id == offering.AdmissionCycleId)?.Name ?? string.Empty,
				Code = offering.Code,
				IsActive = offering.IsActive
			}).ToList()
		};
	}

	private async Task<AdmissionWorkflowRuntimeAnalyticsDto> GetRuntimeAnalyticsAsync(Guid workflowId)
	{
		var workflowQuery = await workflowInstanceRepository.GetQueryableAsync();
		var instances = await AsyncExecuter.ToListAsync(
			workflowQuery.Where(instance => instance.WorkflowDefinitionId == workflowId));
		if (instances.Count == 0) return new AdmissionWorkflowRuntimeAnalyticsDto();

		var instanceIds = instances.Select(instance => instance.Id).ToList();
		var stageQuery = await stageInstanceRepository.GetQueryableAsync();
		var stageInstances = await AsyncExecuter.ToListAsync(
			stageQuery.Where(stage => instanceIds.Contains(stage.WorkflowInstanceId)));
		var completedInstances = instances
			.Where(instance => instance.Status == AdmissionWorkflowInstanceStatus.Completed)
			.ToList();

		return new AdmissionWorkflowRuntimeAnalyticsDto
		{
			StartedCount = instances.Count,
			ActiveCount = instances.Count(instance => instance.Status == AdmissionWorkflowInstanceStatus.Active),
			CompletedCount = completedInstances.Count,
			CompletionRatePercent = Percentage(completedInstances.Count, instances.Count),
			AverageCompletionHours = AverageHours(completedInstances
				.Where(instance => instance.CompletedAt.HasValue)
				.Select(instance => instance.CompletedAt!.Value - instance.StartedAt)),
			Stages = stageInstances
				.Where(stage => stage.EnteredAt.HasValue)
				.GroupBy(stage => stage.DefinitionStageId)
				.Select(group =>
				{
					var first = group.First();
					var completed = group.Count(stage => stage.Status == AdmissionRuntimeStageStatus.Completed);
					var skipped = group.Count(stage => stage.Status == AdmissionRuntimeStageStatus.Skipped);
					var active = group.Where(stage => stage.Status == AdmissionRuntimeStageStatus.Active).ToList();
					return new AdmissionWorkflowStageAnalyticsDto
					{
						DefinitionStageId = group.Key,
						Code = first.Code,
						Name = first.Name,
						StageType = first.StageType,
						DisplayOrder = first.DisplayOrder,
						EnteredCount = group.Count(),
						ActiveCount = active.Count,
						CompletedCount = completed,
						SkippedCount = skipped,
						CompletionRatePercent = Percentage(completed + skipped, group.Count()),
						AverageDurationHours = AverageHours(group
							.Where(stage => stage.EnteredAt.HasValue && stage.CompletedAt.HasValue)
							.Select(stage => stage.CompletedAt!.Value - stage.EnteredAt!.Value)),
						OldestActiveEnteredAt = active.Count == 0
							? null
							: active.Min(stage => stage.EnteredAt)
					};
				})
				.OrderBy(stage => stage.DisplayOrder)
				.ToList()
		};
	}

	private static decimal Percentage(int numerator, int denominator) =>
		denominator == 0 ? 0 : Math.Round(numerator * 100m / denominator, 1);

	private static decimal? AverageHours(IEnumerable<TimeSpan> durations)
	{
		var values = durations.Select(duration => (decimal)duration.TotalHours).ToList();
		return values.Count == 0 ? null : Math.Round(values.Average(), 1);
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
			IsRuntimeReady = metadata.IsRuntimeReady,
			IsSelectable = metadata.IsSelectable,
			IsDeprecated = metadata.IsDeprecated,
			ReplacementGuidance = metadata.ReplacementGuidance
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

	private async Task<Dictionary<Guid, int>> GetCampusCountsAsync(List<Guid> ids)
	{
		var query = await offeringRepository.GetQueryableAsync();
		var counts = await AsyncExecuter.ToListAsync(query
			.Where(offering => offering.WorkflowDefinitionId.HasValue &&
				offering.CampusId.HasValue && ids.Contains(offering.WorkflowDefinitionId.Value))
			.GroupBy(offering => offering.WorkflowDefinitionId!.Value)
			.Select(group => new
			{
				Id = group.Key,
				Count = group.Select(offering => offering.CampusId!.Value).Distinct().Count()
			}));
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
