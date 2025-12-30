using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// Application service for managing earning components
/// </summary>
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
	
	// Base cache key for all active earning components
	private string GetBaseCacheKey() => 
		$"EarningComponents:Active:Tenant:{CurrentTenant?.Id ?? Guid.Empty}";
	
	// Paginated cache key
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
		var earningComponent = await _earningComponentManager.CreateAsync(
			input.Name,
			input.Description,
			input.ComponentType,
			input.DependOn,
			input.CustomComponentId,
			input.Amount,
			input.VisibleInPayslip,
			input.DisplayOrder
		);

		await Repository.InsertAsync(earningComponent, autoSave: true);

		// Clear cache
		await ClearCacheAsync();

		return ObjectMapper.Map<EarningComponent, EarningComponentDto>(earningComponent);
	}

	public override async Task<EarningComponentDto> UpdateAsync(Guid id, UpdateEarningComponentDto input)
	{
		var earningComponent = await Repository.GetAsync(id);

		await _earningComponentManager.UpdateAsync(
			earningComponent,
			input.Name,
			input.Description,
			input.ComponentType,
			input.DependOn,
			input.CustomComponentId,
			input.Amount,
			input.VisibleInPayslip,
			input.DisplayOrder
		);

		await Repository.UpdateAsync(earningComponent, autoSave: true);

		// Clear cache
		await ClearCacheAsync();

		return ObjectMapper.Map<EarningComponent, EarningComponentDto>(earningComponent);
	}

	public override async Task DeleteAsync(Guid id)
	{
		await Repository.DeleteAsync(id);
		
		// Clear cache
		await ClearCacheAsync();
	}

	public override async Task<PagedResultDto<EarningComponentDto>> GetListAsync(PagedAndSortedResultRequestDto input)
	{
		var queryable = await Repository.GetQueryableAsync();
		
		// Apply sorting
		var sorting = input.Sorting ?? "DisplayOrder ASC, Name ASC";
		queryable = ApplySorting(queryable, sorting);

		// Get total count
		var totalCount = await AsyncExecuter.CountAsync(queryable);

		// Get items
		var items = await AsyncExecuter.ToListAsync(
			queryable
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount)
		);

		return new PagedResultDto<EarningComponentDto>(
			totalCount,
			ObjectMapper.Map<List<EarningComponent>, List<EarningComponentDto>>(items)
		);
	}

	public async Task<PagedResultDto<EarningComponentDto>> GetAllActiveAsync(PagedResultRequestDto input)
	{
		// Try to get from cache
		var activeCacheKey = GetActiveCacheKey(input.SkipCount, input.MaxResultCount);
		var cached = await _cache.GetAsync(activeCacheKey);
		if (cached != null)
		{
			return cached;
		}

		// Fetch from database
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

		// Cache the result
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
		
		// Clear cache
		await ClearCacheAsync();
	}

	public async Task DeactivateAsync(Guid id)
	{
		var earningComponent = await Repository.GetAsync(id);
		earningComponent.Deactivate();
		await Repository.UpdateAsync(earningComponent, autoSave: true);
		
		// Clear cache
		await ClearCacheAsync();
	}

	private async Task ClearCacheAsync()
	{
		// Clear cache by removing common pagination combinations
		// Since IDistributedCache doesn't support pattern-based removal out of the box,
		// we generate cache keys for common pagination scenarios
		
		var baseCacheKey = GetBaseCacheKey();
		var cacheKeysToRemove = new List<string>();
		
		// Common page sizes used in the application
		int[] commonPageSizes = { 10, 25, 50, 100 };
		
		// For each page size, clear first few pages (covers most access patterns)
		// Adjust maxPages based on your typical data volume
		int maxPages = 10;
		
		foreach (var pageSize in commonPageSizes)
		{
			for (int page = 0; page < maxPages; page++)
			{
				int skipCount = page * pageSize;
				cacheKeysToRemove.Add(GetActiveCacheKey(skipCount, pageSize));
			}
		}
		
		// Use RemoveManyAsync for better performance (single operation vs multiple awaits)
		await _cache.RemoveManyAsync(cacheKeysToRemove);
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
				// First sort: use OrderBy/OrderByDescending
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
				// Subsequent sorts: use ThenBy/ThenByDescending
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

		// Apply default fallback if no valid sort was applied
		return orderedQueryable ?? queryable.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name);
	}
}
