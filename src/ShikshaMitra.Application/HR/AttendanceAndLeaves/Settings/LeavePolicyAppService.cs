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

public class LeavePolicyAppService : CrudAppService<
	LeavePolicy,
	LeavePolicyDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateLeavePolicyDto,
	UpdateLeavePolicyDto>, ILeavePolicyAppService
{
	private readonly LeavePolicyManager _leavePolicyManager;
	private readonly IDistributedCache<PagedResultDto<LeavePolicyDto>> _cache;
	
	// Base cache key for all active leave policies
	private string GetBaseCacheKey() => 
		$"EmployeeLeavePolicies:Active:Tenant:{CurrentTenant?.Id ?? Guid.Empty}";
	
	// Paginated cache key
	private string GetActiveCacheKey(int skipCount, int maxResultCount) => 
		$"{GetBaseCacheKey()}:Skip:{skipCount}:Take:{maxResultCount}";

	public LeavePolicyAppService(
		IRepository<LeavePolicy, Guid> repository,
		LeavePolicyManager leavePolicyManager,
		IDistributedCache<PagedResultDto<LeavePolicyDto>> cache)
		: base(repository)
	{
		_leavePolicyManager = leavePolicyManager;
		_cache = cache;
	}

	public override async Task<LeavePolicyDto> CreateAsync(CreateLeavePolicyDto input)
	{
		var policy = await _leavePolicyManager.CreateAsync(
			input.PolicyName,
			input.LeaveType,
			input.MaximumDaysPerYear,
			input.PaidType,
			input.CarryForward,
			input.Encashable,
			input.RequiresApproval,
			input.Description
		);
		await Repository.InsertAsync(policy, autoSave: true);
		await ClearCacheAsync();
		return ObjectMapper.Map<LeavePolicy, LeavePolicyDto>(policy);
	}

	public override async Task<LeavePolicyDto> UpdateAsync(Guid id, UpdateLeavePolicyDto input)
	{
		var policy = await Repository.GetAsync(id);
		await _leavePolicyManager.UpdateAsync(
			policy,
			input.PolicyName,
			input.LeaveType,
			input.MaximumDaysPerYear,
			input.PaidType,
			input.CarryForward,
			input.Encashable,
			input.RequiresApproval,
			input.Description
		);
		await Repository.UpdateAsync(policy, autoSave: true);
		await ClearCacheAsync();
		return ObjectMapper.Map<LeavePolicy, LeavePolicyDto>(policy);
	}

	public override async Task DeleteAsync(Guid id)
	{
		// Use soft delete to preserve historical data integrity
		var leavePolicy = await Repository.GetAsync(id);
		leavePolicy.Deactivate();
		await Repository.UpdateAsync(leavePolicy, autoSave: true);
		await ClearCacheAsync();
	}

	public override async Task<PagedResultDto<LeavePolicyDto>> GetListAsync(PagedAndSortedResultRequestDto input)
	{
		var queryable = await Repository.GetQueryableAsync();
		
		// Apply sorting
		var sorting = input.Sorting ?? "PolicyName ASC";
		queryable = ApplySorting(queryable, sorting);

		// Get total count
		var totalCount = await AsyncExecuter.CountAsync(queryable);

		// Get items
		var items = await AsyncExecuter.ToListAsync(
			queryable
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount)
		);

		return new PagedResultDto<LeavePolicyDto>(
			totalCount,
			ObjectMapper.Map<List<LeavePolicy>, List<LeavePolicyDto>>(items)
		);
	}

	public async Task<PagedResultDto<LeavePolicyDto>> GetAllActiveAsync(PagedResultRequestDto input)
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
			query.OrderBy(x => x.PolicyName)
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount)
		);

		var result = new PagedResultDto<LeavePolicyDto>(
			totalCount,
			ObjectMapper.Map<List<LeavePolicy>, List<LeavePolicyDto>>(items)
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

	private IQueryable<LeavePolicy> ApplySorting(IQueryable<LeavePolicy> queryable, string sorting)
	{
		if (string.IsNullOrWhiteSpace(sorting))
		{
			return queryable.OrderBy(x => x.PolicyName);
		}

		var sortingParts = sorting.Split(',');
		IOrderedQueryable<LeavePolicy>? orderedQueryable = null;
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
					"policyname" => direction == "DESC" 
						? queryable.OrderByDescending(x => x.PolicyName) 
						: queryable.OrderBy(x => x.PolicyName),
					"leavetype" => direction == "DESC"
						? queryable.OrderByDescending(x => x.LeaveType)
						: queryable.OrderBy(x => x.LeaveType),
					"maximumdaysperyear" => direction == "DESC"
						? queryable.OrderByDescending(x => x.MaximumDaysPerYear)
						: queryable.OrderBy(x => x.MaximumDaysPerYear),
					"isactive" => direction == "DESC"
						? queryable.OrderByDescending(x => x.IsActive)
						: queryable.OrderBy(x => x.IsActive),
					_ => queryable.OrderBy(x => x.PolicyName)
				};
				isFirstSort = false;
			}
			else
			{
				// Subsequent sorts: use ThenBy/ThenByDescending
				orderedQueryable = property.ToLower() switch
				{
					"policyname" => direction == "DESC" 
						? orderedQueryable!.ThenByDescending(x => x.PolicyName) 
						: orderedQueryable!.ThenBy(x => x.PolicyName),
					"leavetype" => direction == "DESC"
						? orderedQueryable!.ThenByDescending(x => x.LeaveType)
						: orderedQueryable!.ThenBy(x => x.LeaveType),
					"maximumdaysperyear" => direction == "DESC"
						? orderedQueryable!.ThenByDescending(x => x.MaximumDaysPerYear)
						: orderedQueryable!.ThenBy(x => x.MaximumDaysPerYear),
					"isactive" => direction == "DESC"
						? orderedQueryable!.ThenByDescending(x => x.IsActive)
						: orderedQueryable!.ThenBy(x => x.IsActive),
					_ => orderedQueryable!.ThenBy(x => x.PolicyName)
				};
			}
		}

		// Apply default fallback if no valid sort was applied
		return orderedQueryable ?? queryable.OrderBy(x => x.PolicyName);
	}
}