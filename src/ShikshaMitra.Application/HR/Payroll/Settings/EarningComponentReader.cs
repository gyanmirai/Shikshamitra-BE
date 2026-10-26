using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.ObjectMapping;

namespace ShikshaMitra.HR.Payroll.Settings;

public class EarningComponentReader(
	IRepository<EarningComponent, Guid> repository,
	IDistributedCache<PagedResultDto<EarningComponentDto>> cache,
	ICurrentTenant currentTenant,
	IObjectMapper objectMapper,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	private const string CachePrefix = "EarningComponents";

	public async Task<EarningComponentDto> GetAsync(Guid id)
	{
		var earningComponent = await repository.GetAsync(id);
		var dto = objectMapper.Map<EarningComponent, EarningComponentDto>(earningComponent);
		return await PopulateSelectedComponentDataAsync(dto);
	}

	public async Task<PagedResultDto<EarningComponentDto>> GetFilteredListAsync(
		int skipCount,
		int maxResultCount,
		string? search)
	{
		var queryable = (await repository.GetQueryableAsync()).Where(x => x.IsActive);

		if (!string.IsNullOrWhiteSpace(search))
		{
			var term = search.Trim().ToLower();
			queryable = queryable.Where(x => x.Name.ToLower().Contains(term));
		}

		queryable = queryable.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).ThenBy(x => x.Id);

		var totalCount = await asyncExecuter.CountAsync(queryable);
		var items = await asyncExecuter.ToListAsync(
			queryable
				.Skip(skipCount)
				.Take(maxResultCount));

		var mappedItems = objectMapper.Map<List<EarningComponent>, List<EarningComponentDto>>(items);
		await PopulateSelectedComponentDataAsync(mappedItems);

		return new PagedResultDto<EarningComponentDto>(totalCount, mappedItems);
	}

	public async Task<PagedResultDto<EarningComponentDto>> GetAllActiveAsync(PagedResultRequestDto input)
	{
		var activeCacheKey = PayrollComponentImpactProjection.BuildActiveCacheKey(
			CachePrefix,
			currentTenant.Id,
			input.SkipCount,
			input.MaxResultCount);

		var cached = await cache.GetAsync(activeCacheKey);
		if (cached != null)
		{
			return cached;
		}

		var queryable = await repository.GetQueryableAsync();
		var query = queryable.Where(x => x.IsActive);

		var totalCount = await asyncExecuter.CountAsync(query);
		var items = await asyncExecuter.ToListAsync(
			query.OrderBy(x => x.DisplayOrder)
				.ThenBy(x => x.Name).ThenBy(x => x.Id)
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount));

		var result = new PagedResultDto<EarningComponentDto>(
			totalCount,
			objectMapper.Map<List<EarningComponent>, List<EarningComponentDto>>(items));

		await PopulateSelectedComponentDataAsync(result.Items);

		await cache.SetAsync(
			activeCacheKey,
			result,
			new DistributedCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
			});

		return result;
	}

	private async Task<EarningComponentDto> PopulateSelectedComponentDataAsync(EarningComponentDto dto)
	{
		if (!dto.SelectedEarningComponentId.HasValue)
		{
			return dto;
		}

		var selected = await repository.FindAsync(dto.SelectedEarningComponentId.Value);
		if (selected is null)
		{
			return dto;
		}

		dto.SelectedEarningComponentName = selected.Name;
		dto.SelectedEarningComponentDescription = selected.Description;
		dto.SelectedEarningComponentVisibleInPayslip = selected.VisibleInPayslip;

		return dto;
	}

	private async Task PopulateSelectedComponentDataAsync(IReadOnlyList<EarningComponentDto> items)
	{
		var selectedIds = items
			.Where(x => x.SelectedEarningComponentId.HasValue)
			.Select(x => x.SelectedEarningComponentId!.Value)
			.Distinct()
			.ToList();

		if (selectedIds.Count == 0)
		{
			return;
		}

		var queryable = await repository.GetQueryableAsync();
		var selectedComponents = await asyncExecuter.ToListAsync(
			queryable.Where(x => selectedIds.Contains(x.Id)));

		var selectedById = selectedComponents.ToDictionary(x => x.Id);

		foreach (var item in items)
		{
			if (!item.SelectedEarningComponentId.HasValue)
			{
				continue;
			}

			if (!selectedById.TryGetValue(item.SelectedEarningComponentId.Value, out var selected))
			{
				continue;
			}

			item.SelectedEarningComponentName = selected.Name;
			item.SelectedEarningComponentDescription = selected.Description;
			item.SelectedEarningComponentVisibleInPayslip = selected.VisibleInPayslip;
		}
	}
}
