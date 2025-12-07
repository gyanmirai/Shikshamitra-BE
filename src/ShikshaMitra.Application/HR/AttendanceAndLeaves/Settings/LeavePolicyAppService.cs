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
		return MapToDto(policy);
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
		return MapToDto(policy);
	}

	public override async Task DeleteAsync(Guid id)
	{
		await Repository.DeleteAsync(id);
		await ClearCacheAsync();
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
			items.Select(MapToDto).ToList()
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

	protected override async Task<LeavePolicyDto> MapToGetOutputDtoAsync(LeavePolicy entity)
	{
		return MapToDto(entity);
	}

	protected override async Task<LeavePolicyDto> MapToGetListOutputDtoAsync(LeavePolicy entity)
	{
		return MapToDto(entity);
	}

	private LeavePolicyDto MapToDto(LeavePolicy policy)
	{
		return new LeavePolicyDto
		{
			Id = policy.Id,
			PolicyName = policy.PolicyName,
			LeaveType = policy.LeaveType,
			MaximumDaysPerYear = policy.MaximumDaysPerYear,
			PaidType = policy.PaidType == LeavePaidType.Paid ? "Paid" : "Unpaid",
			CarryForward = policy.CarryForward,
			Encashable = policy.Encashable,
			RequiresApproval = policy.RequiresApproval,
			Description = policy.Description,
			IsActive = policy.IsActive,
			CreationTime = policy.CreationTime,
			CreatorId = policy.CreatorId,
			LastModificationTime = policy.LastModificationTime,
			LastModifierId = policy.LastModifierId
		};
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
}