using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class LeaveTypeAppService : CrudAppService<
	LeaveType,
	LeaveTypeDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateLeaveTypeDto,
	UpdateLeaveTypeDto>, ILeaveTypeAppService
{
	private readonly LeaveTypeManager _leaveTypeManager;
	private readonly IDistributedCache<PagedResultDto<LeaveTypeDto>> _cache;
	
	// Base cache key for all active leave types (unpaginated)
	private string GetBaseCacheKey() => 
		$"EmployeeLeaveTypes:Active:Tenant:{CurrentTenant?.Id ?? Guid.Empty}";
	
	// Paginated cache key
	private string GetActiveCacheKey(int skipCount, int maxResultCount) => 
		$"{GetBaseCacheKey()}:Skip:{skipCount}:Take:{maxResultCount}";

	public LeaveTypeAppService(
		IRepository<LeaveType, Guid> repository,
		LeaveTypeManager leaveTypeManager,
		IDistributedCache<PagedResultDto<LeaveTypeDto>> cache)
		: base(repository)
	{
		_leaveTypeManager = leaveTypeManager;
		_cache = cache;
	}

	public override async Task<LeaveTypeDto> CreateAsync(CreateLeaveTypeDto input)
	{
		var leaveType = await _leaveTypeManager.CreateAsync(input.Type, input.Color);
		await Repository.InsertAsync(leaveType, autoSave: true);
		await ClearCacheAsync();
		return ObjectMapper.Map<LeaveType, LeaveTypeDto>(leaveType);
	}

	public override async Task<LeaveTypeDto> UpdateAsync(Guid id, UpdateLeaveTypeDto input)
	{
		var leaveType = await Repository.GetAsync(id);
		await _leaveTypeManager.UpdateAsync(leaveType, input.Type, input.Color);
		await Repository.UpdateAsync(leaveType, autoSave: true);
		await ClearCacheAsync();
		return ObjectMapper.Map<LeaveType, LeaveTypeDto>(leaveType);
	}

	public override async Task DeleteAsync(Guid id)
	{
		var leaveType = await Repository.GetAsync(id);
		leaveType.Deactivate();
		await Repository.UpdateAsync(leaveType, autoSave: true);
		await ClearCacheAsync();
	}

	public override async Task<PagedResultDto<LeaveTypeDto>> GetListAsync(PagedAndSortedResultRequestDto input)
	{
		var queryable = await Repository.GetQueryableAsync();
		
		// Apply sorting
		var sorting = input.Sorting ?? "Type ASC";
		queryable = ApplySorting(queryable, sorting);

		// Get total count
		var totalCount = await AsyncExecuter.CountAsync(queryable);

		// Get items
		var items = await AsyncExecuter.ToListAsync(
			queryable
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount)
		);

		return new PagedResultDto<LeaveTypeDto>(
			totalCount,
			ObjectMapper.Map<List<LeaveType>, List<LeaveTypeDto>>(items)
		);
	}

	public async Task<PagedResultDto<LeaveTypeDto>> GetAllActiveAsync(PagedResultRequestDto input)
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
			query.OrderBy(x => x.Type)
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount)
		);

		var result = new PagedResultDto<LeaveTypeDto>(
			totalCount,
			ObjectMapper.Map<List<LeaveType>, List<LeaveTypeDto>>(items)
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

	private IQueryable<LeaveType> ApplySorting(IQueryable<LeaveType> queryable, string sorting)
	{
		if (string.IsNullOrWhiteSpace(sorting))
		{
			return queryable.OrderBy(x => x.Type);
		}

		var sortingParts = sorting.Split(',');
		IOrderedQueryable<LeaveType>? orderedQueryable = null;
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
					"type" => direction == "DESC" 
						? queryable.OrderByDescending(x => x.Type) 
						: queryable.OrderBy(x => x.Type),
					"color" => direction == "DESC"
						? queryable.OrderByDescending(x => x.Color)
						: queryable.OrderBy(x => x.Color),
					"isactive" => direction == "DESC"
						? queryable.OrderByDescending(x => x.IsActive)
						: queryable.OrderBy(x => x.IsActive),
					_ => queryable.OrderBy(x => x.Type)
				};
				isFirstSort = false;
			}
			else
			{
				// Subsequent sorts: use ThenBy/ThenByDescending
				orderedQueryable = property.ToLower() switch
				{
					"type" => direction == "DESC" 
						? orderedQueryable!.ThenByDescending(x => x.Type) 
						: orderedQueryable!.ThenBy(x => x.Type),
					"color" => direction == "DESC"
						? orderedQueryable!.ThenByDescending(x => x.Color)
						: orderedQueryable!.ThenBy(x => x.Color),
					"isactive" => direction == "DESC"
						? orderedQueryable!.ThenByDescending(x => x.IsActive)
						: orderedQueryable!.ThenBy(x => x.IsActive),
					_ => orderedQueryable!.ThenBy(x => x.Type)
				};
			}
		}

		// Apply default fallback if no valid sort was applied
		return orderedQueryable ?? queryable.OrderBy(x => x.Type);
	}
}