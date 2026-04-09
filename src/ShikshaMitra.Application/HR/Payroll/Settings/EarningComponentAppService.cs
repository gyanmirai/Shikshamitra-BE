using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.Settings;

[RemoteService(false)]
public class EarningComponentAppService : CrudAppService<
	EarningComponent,
	EarningComponentDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateEarningComponentDto,
	UpdateEarningComponentDto>, IEarningComponentAppService
{
	private readonly EarningComponentManager _earningComponentManager;
	private readonly IDistributedCache<PagedResultDto<EarningComponentDto>> _cache;

	private string GetBaseCacheKey() =>
		$"EarningComponents:Active:Tenant:{CurrentTenant?.Id ?? Guid.Empty}";

	private string GetActiveCacheKey(int skipCount, int maxResultCount) =>
		$"{GetBaseCacheKey()}:Skip:{skipCount}:Take:{maxResultCount}";

	public EarningComponentAppService(
		IRepository<EarningComponent, Guid> repository,
		EarningComponentManager earningComponentManager,
		IDistributedCache<PagedResultDto<EarningComponentDto>> cache)
		: base(repository)
	{
		_earningComponentManager = earningComponentManager;
		_cache = cache;
	}

	public override async Task<EarningComponentDto> CreateAsync(CreateEarningComponentDto input)
	{
		ArgumentNullException.ThrowIfNull(input);

		var earningComponent = await _earningComponentManager.CreateAsync(
			input.Name,
			input.Description,
			input.ComponentType,
			input.CalculationType,
			input.Amount,
			input.Percentage,
			input.SelectedEarningComponentId,
			input.DependOn,
			input.VisibleInPayslip,
			input.DisplayOrder
		);

		try
		{
			await Repository.InsertAsync(earningComponent, autoSave: true);
		}
		catch (Exception ex) when (IsEarningComponentNameUniqueViolation(ex))
		{
			throw new BusinessException("ShikshaMitra:EarningComponentNameAlreadyExists")
				.WithData("name", input.Name);
		}

		await ClearCacheAsync();

		return ObjectMapper.Map<EarningComponent, EarningComponentDto>(earningComponent);
	}

	public override async Task<EarningComponentDto> UpdateAsync(Guid id, UpdateEarningComponentDto input)
	{
		ArgumentNullException.ThrowIfNull(input);

		var earningComponent = await Repository.GetAsync(id);

		await _earningComponentManager.UpdateAsync(
			earningComponent,
			input.Name,
			input.Description,
			input.ComponentType,
			input.CalculationType,
			input.Amount,
			input.Percentage,
			input.SelectedEarningComponentId,
			input.DependOn,
			input.VisibleInPayslip,
			input.DisplayOrder
		);

		try
		{
			await Repository.UpdateAsync(earningComponent, autoSave: true);
		}
		catch (Exception ex) when (IsEarningComponentNameUniqueViolation(ex))
		{
			throw new BusinessException("ShikshaMitra:EarningComponentNameAlreadyExists")
				.WithData("name", input.Name);
		}

		await ClearCacheAsync();

		return ObjectMapper.Map<EarningComponent, EarningComponentDto>(earningComponent);
	}

	public override async Task DeleteAsync(Guid id)
	{
		await Repository.DeleteAsync(id);
		await ClearCacheAsync();
	}

	public override async Task<PagedResultDto<EarningComponentDto>> GetListAsync(PagedAndSortedResultRequestDto input)
	{
		return await GetFilteredListAsync(input.SkipCount, input.MaxResultCount, null);
	}

	public override async Task<EarningComponentDto> GetAsync(Guid id)
	{
		var dto = await base.GetAsync(id);
		return await PopulateSelectedComponentDataAsync(dto);
	}

	public async Task<PagedResultDto<EarningComponentDto>> GetFilteredListAsync(int skipCount, int maxResultCount, string? search)
	{
		var queryable = await Repository.GetQueryableAsync();

		if (!string.IsNullOrWhiteSpace(search))
		{
			var term = search.Trim().ToLower();
			queryable = queryable.Where(x => x.Name.ToLower().Contains(term));
		}

		queryable = queryable.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name);

		var totalCount = await AsyncExecuter.CountAsync(queryable);

		var items = await AsyncExecuter.ToListAsync(
			queryable
				.Skip(skipCount)
				.Take(maxResultCount)
		);

		var mappedItems = ObjectMapper.Map<List<EarningComponent>, List<EarningComponentDto>>(items);
		await PopulateSelectedComponentDataAsync(mappedItems);

		return new PagedResultDto<EarningComponentDto>(totalCount, mappedItems);
	}

	public async Task<PagedResultDto<EarningComponentDto>> GetAllActiveAsync(PagedResultRequestDto input)
	{
		var activeCacheKey = GetActiveCacheKey(input.SkipCount, input.MaxResultCount);
		var cached = await _cache.GetAsync(activeCacheKey);
		if (cached != null)
		{
			return cached;
		}

		var queryable = await Repository.GetQueryableAsync();
		var query = queryable.Where(x => x.IsActive);

		var totalCount = await AsyncExecuter.CountAsync(query);
		var items = await AsyncExecuter.ToListAsync(
			query.OrderBy(x => x.DisplayOrder)
				.ThenBy(x => x.Name)
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount)
		);

		var result = new PagedResultDto<EarningComponentDto>(
			totalCount,
			ObjectMapper.Map<List<EarningComponent>, List<EarningComponentDto>>(items)
		);

		await PopulateSelectedComponentDataAsync(result.Items);

		await _cache.SetAsync(
			activeCacheKey,
			result,
			new DistributedCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
			}
		);

		return result;
	}

	public async Task ActivateAsync(Guid id)
	{
		var earningComponent = await Repository.GetAsync(id);
		earningComponent.Activate();
		await Repository.UpdateAsync(earningComponent, autoSave: true);
		await ClearCacheAsync();
	}

	public async Task DeactivateAsync(Guid id)
	{
		var earningComponent = await Repository.GetAsync(id);
		earningComponent.Deactivate();
		await Repository.UpdateAsync(earningComponent, autoSave: true);
		await ClearCacheAsync();
	}

	private async Task ClearCacheAsync()
	{
		var cacheKeysToRemove = new List<string>();
		int[] commonPageSizes = { 10, 20, 25, 50, 100 };
		int maxPages = 10;

		foreach (var pageSize in commonPageSizes)
		{
			for (int page = 0; page < maxPages; page++)
			{
				int skipCount = page * pageSize;
				cacheKeysToRemove.Add(GetActiveCacheKey(skipCount, pageSize));
			}
		}

		try
		{
			await _cache.RemoveManyAsync(cacheKeysToRemove);
		}
		catch (Exception ex)
		{
			Logger.LogWarning(
				ex,
				"Failed to invalidate earning component cache for tenant {TenantId}. List endpoints may be stale until expiration.",
				CurrentTenant?.Id);
		}
	}

	/// <summary>
	/// Maps DB unique violations on (TenantId, Name) to a handled business error (e.g. race after <see cref="EarningComponentManager"/> check).
	/// </summary>
	private static bool IsEarningComponentNameUniqueViolation(Exception exception)
	{
		const string indexName = "IX_EarningComponents_TenantId_Name";
		const string tableHint = "AppEarningComponents";

		for (var ex = exception; ex != null; ex = ex.InnerException!)
		{
			var message = ex.Message;
			if (string.IsNullOrEmpty(message))
			{
				continue;
			}

			if (message.Contains(indexName, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (message.Contains(tableHint, StringComparison.OrdinalIgnoreCase)
			    && message.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			// PostgreSQL: 23505 = unique_violation
			if (message.Contains("23505", StringComparison.Ordinal)
			    && (message.Contains(tableHint, StringComparison.OrdinalIgnoreCase)
			        || message.Contains(indexName, StringComparison.OrdinalIgnoreCase)))
			{
				return true;
			}
		}

		return false;
	}

	private IQueryable<EarningComponent> ApplySorting(IQueryable<EarningComponent> queryable, string sorting)
	{
		if (string.IsNullOrWhiteSpace(sorting))
		{
			return queryable.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name);
		}

		var sortingParts = sorting.Split(',');
		IOrderedQueryable<EarningComponent>? orderedQueryable = null;
		bool isFirstSort = true;

		foreach (var sortPart in sortingParts)
		{
			var parts = sortPart.Trim().Split(' ');
			var property = parts[0];
			var direction = parts.Length > 1 ? parts[1].ToUpper() : "ASC";

			if (isFirstSort)
			{
				orderedQueryable = property.ToLower() switch
				{
					"name" => direction == "DESC"
						? queryable.OrderByDescending(x => x.Name)
						: queryable.OrderBy(x => x.Name),
					"componenttype" => direction == "DESC"
						? queryable.OrderByDescending(x => x.ComponentType)
						: queryable.OrderBy(x => x.ComponentType),
					"displayorder" => direction == "DESC"
						? queryable.OrderByDescending(x => x.DisplayOrder)
						: queryable.OrderBy(x => x.DisplayOrder),
					"isactive" => direction == "DESC"
						? queryable.OrderByDescending(x => x.IsActive)
						: queryable.OrderBy(x => x.IsActive),
					_ => queryable.OrderBy(x => x.DisplayOrder)
				};
				isFirstSort = false;
			}
			else
			{
				orderedQueryable = property.ToLower() switch
				{
					"name" => direction == "DESC"
						? orderedQueryable!.ThenByDescending(x => x.Name)
						: orderedQueryable!.ThenBy(x => x.Name),
					"componenttype" => direction == "DESC"
						? orderedQueryable!.ThenByDescending(x => x.ComponentType)
						: orderedQueryable!.ThenBy(x => x.ComponentType),
					"displayorder" => direction == "DESC"
						? orderedQueryable!.ThenByDescending(x => x.DisplayOrder)
						: orderedQueryable!.ThenBy(x => x.DisplayOrder),
					"isactive" => direction == "DESC"
						? orderedQueryable!.ThenByDescending(x => x.IsActive)
						: orderedQueryable!.ThenBy(x => x.IsActive),
					_ => orderedQueryable!.ThenBy(x => x.Name)
				};
			}
		}

		return orderedQueryable ?? queryable.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name);
	}

	private async Task<EarningComponentDto> PopulateSelectedComponentDataAsync(EarningComponentDto dto)
	{
		if (!dto.SelectedEarningComponentId.HasValue)
		{
			return dto;
		}

		var selected = await Repository.FindAsync(dto.SelectedEarningComponentId.Value);
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

		var queryable = await Repository.GetQueryableAsync();
		var selectedComponents = await AsyncExecuter.ToListAsync(
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
