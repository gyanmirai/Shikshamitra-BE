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
	private string GetActiveCacheKey(int skipCount, int maxResultCount) => 
		$"EmployeeLeavePolicies:Active:Tenant:{CurrentTenant?.Id ?? Guid.Empty}:Skip:{skipCount}:Take:{maxResultCount}";

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
		// Clear all pagination cache entries by removing with pattern matching
		// Note: This is a simplified approach. For production, consider using cache tags or patterns
		var baseCacheKey = $"EmployeeLeavePolicies:Active:Tenant:{CurrentTenant?.Id ?? Guid.Empty}";
		// Since IDistributedCache doesn't support pattern removal, we'll need to clear specific common pagination keys
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
}