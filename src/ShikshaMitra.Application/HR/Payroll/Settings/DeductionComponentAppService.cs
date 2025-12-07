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
/// Application service for managing deduction components
/// </summary>
public class DeductionComponentAppService : CrudAppService<
	DeductionComponent,
	DeductionComponentDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateDeductionComponentDto,
	UpdateDeductionComponentDto>, IDeductionComponentAppService
{
	private readonly DeductionComponentManager _deductionComponentManager;
	private readonly IDistributedCache<PagedResultDto<DeductionComponentDto>> _cache;
	private string GetActiveCacheKey(int skipCount, int maxResultCount) => 
		$"DeductionComponents:Active:Tenant:{CurrentTenant?.Id ?? Guid.Empty}:Skip:{skipCount}:Take:{maxResultCount}";

	public DeductionComponentAppService(
		IRepository<DeductionComponent, Guid> repository,
		DeductionComponentManager deductionComponentManager,
		IDistributedCache<PagedResultDto<DeductionComponentDto>> cache)
		: base(repository)
	{
		_deductionComponentManager = deductionComponentManager;
		_cache = cache;
	}

	public override async Task<DeductionComponentDto> CreateAsync(CreateDeductionComponentDto input)
	{
		var deductionComponent = await _deductionComponentManager.CreateAsync(
			input.Name,
			input.Description,
			input.ComponentType,
			input.DependOn,
			input.Amount,
			input.VisibleInPayslip,
			input.DisplayOrder
		);

		await Repository.InsertAsync(deductionComponent, autoSave: true);
		
		// Clear cache
		await ClearCacheAsync();

		return ObjectMapper.Map<DeductionComponent, DeductionComponentDto>(deductionComponent);
	}

	public override async Task<DeductionComponentDto> UpdateAsync(Guid id, UpdateDeductionComponentDto input)
	{
		var deductionComponent = await Repository.GetAsync(id);

		await _deductionComponentManager.UpdateAsync(
			deductionComponent,
			input.Name,
			input.Description,
			input.ComponentType,
			input.DependOn,
			input.Amount,
			input.VisibleInPayslip,
			input.DisplayOrder
		);

		await Repository.UpdateAsync(deductionComponent, autoSave: true);
		
		// Clear cache
		await ClearCacheAsync();

		return ObjectMapper.Map<DeductionComponent, DeductionComponentDto>(deductionComponent);
	}

	public override async Task DeleteAsync(Guid id)
	{
		await Repository.DeleteAsync(id);
		
		// Clear cache
		await ClearCacheAsync();
	}

	public override async Task<PagedResultDto<DeductionComponentDto>> GetListAsync(PagedAndSortedResultRequestDto input)
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

		return new PagedResultDto<DeductionComponentDto>(
			totalCount,
			ObjectMapper.Map<List<DeductionComponent>, List<DeductionComponentDto>>(items)
		);
	}

	public async Task<PagedResultDto<DeductionComponentDto>> GetAllActiveAsync(PagedResultRequestDto input)
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

		var result = new PagedResultDto<DeductionComponentDto>(
			totalCount,
			ObjectMapper.Map<List<DeductionComponent>, List<DeductionComponentDto>>(items)
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
		var deductionComponent = await Repository.GetAsync(id);
		deductionComponent.Activate();
		await Repository.UpdateAsync(deductionComponent, autoSave: true);
		
		// Clear cache
		await ClearCacheAsync();
	}

	public async Task DeactivateAsync(Guid id)
	{
		var deductionComponent = await Repository.GetAsync(id);
		deductionComponent.Deactivate();
		await Repository.UpdateAsync(deductionComponent, autoSave: true);
		
		// Clear cache
		await ClearCacheAsync();
	}

	private async Task ClearCacheAsync()
	{
		// Clear all pagination cache entries by removing common pagination keys
		// Note: This is a simplified approach. For production, consider using cache tags or patterns
		var commonPaginationKeys = new[]
		{
			GetActiveCacheKey(0, 10),
			GetActiveCacheKey(0, 25),
			GetActiveCacheKey(0, 50),
			GetActiveCacheKey(0, 100)
		};

		foreach (var key in commonPaginationKeys)
		{
			await _cache.RemoveAsync(key);
		}
	}

	private IQueryable<DeductionComponent> ApplySorting(IQueryable<DeductionComponent> queryable, string sorting)
	{
		if (string.IsNullOrWhiteSpace(sorting))
		{
			return queryable.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name);
		}

		var sortingParts = sorting.Split(',');
		foreach (var sortPart in sortingParts)
		{
			var parts = sortPart.Trim().Split(' ');
			var property = parts[0];
			var direction = parts.Length > 1 ? parts[1].ToUpper() : "ASC";

			queryable = property.ToLower() switch
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
				_ => queryable.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name)
			};
		}

		return queryable;
	}
}
