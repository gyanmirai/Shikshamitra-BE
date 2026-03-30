using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.Settings;

[RemoteService(false)]
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

	private string GetBaseCacheKey() =>
		$"DeductionComponents:Active:Tenant:{CurrentTenant?.Id ?? Guid.Empty}";

	private string GetActiveCacheKey(int skipCount, int maxResultCount) =>
		$"{GetBaseCacheKey()}:Skip:{skipCount}:Take:{maxResultCount}";

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
			input.CalculationType,
			input.Amount,
			input.Percentage,
			input.SelectedDeductionComponentId,
			input.DependOn,
			input.VisibleInPayslip,
			input.DisplayOrder
		);

		await Repository.InsertAsync(deductionComponent, autoSave: true);
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
			input.CalculationType,
			input.Amount,
			input.Percentage,
			input.SelectedDeductionComponentId,
			input.DependOn,
			input.VisibleInPayslip,
			input.DisplayOrder
		);

		await Repository.UpdateAsync(deductionComponent, autoSave: true);
		await ClearCacheAsync();

		return ObjectMapper.Map<DeductionComponent, DeductionComponentDto>(deductionComponent);
	}

	public override async Task DeleteAsync(Guid id)
	{
		await Repository.DeleteAsync(id);
		await ClearCacheAsync();
	}

	public override async Task<PagedResultDto<DeductionComponentDto>> GetListAsync(PagedAndSortedResultRequestDto input)
	{
		return await GetFilteredListAsync(input.SkipCount, input.MaxResultCount, null);
	}

	public async Task<PagedResultDto<DeductionComponentDto>> GetFilteredListAsync(int skipCount, int maxResultCount, string? search)
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

		return new PagedResultDto<DeductionComponentDto>(
			totalCount,
			ObjectMapper.Map<List<DeductionComponent>, List<DeductionComponentDto>>(items)
		);
	}

	public async Task<PagedResultDto<DeductionComponentDto>> GetAllActiveAsync(PagedResultRequestDto input)
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

		var result = new PagedResultDto<DeductionComponentDto>(
			totalCount,
			ObjectMapper.Map<List<DeductionComponent>, List<DeductionComponentDto>>(items)
		);

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
		await ClearCacheAsync();
	}

	public async Task DeactivateAsync(Guid id)
	{
		var deductionComponent = await Repository.GetAsync(id);
		deductionComponent.Deactivate();
		await Repository.UpdateAsync(deductionComponent, autoSave: true);
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

		await _cache.RemoveManyAsync(cacheKeysToRemove);
	}

	private IQueryable<DeductionComponent> ApplySorting(IQueryable<DeductionComponent> queryable, string sorting)
	{
		if (string.IsNullOrWhiteSpace(sorting))
		{
			return queryable.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name);
		}

		var sortingParts = sorting.Split(',');
		IOrderedQueryable<DeductionComponent>? orderedQueryable = null;
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
}
