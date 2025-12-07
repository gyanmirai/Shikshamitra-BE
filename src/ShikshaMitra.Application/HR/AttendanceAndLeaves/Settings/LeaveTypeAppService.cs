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
	private string GetActiveCacheKey(int skipCount, int maxResultCount) => 
		$"EmployeeLeaveTypes:Active:Tenant:{CurrentTenant?.Id ?? Guid.Empty}:Skip:{skipCount}:Take:{maxResultCount}";

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
		await Repository.DeleteAsync(id);
		await ClearCacheAsync();
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
		// Clear all pagination cache entries by removing with pattern matching
		// Note: This is a simplified approach. For production, consider using cache tags or patterns
		var baseCacheKey = $"EmployeeLeaveTypes:Active:Tenant:{CurrentTenant?.Id ?? Guid.Empty}";
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