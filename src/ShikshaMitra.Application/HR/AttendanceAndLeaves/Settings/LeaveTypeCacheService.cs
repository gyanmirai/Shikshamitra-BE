using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class LeaveTypeCacheService(
	ICurrentTenant currentTenant,
	IDistributedCache<PagedResultDto<LeaveTypeDto>> activeCache,
	IDistributedCache<LeaveTypeListResponse> contractCache) : ITransientDependency
{
	private static readonly int[] CommonPageSizes = [10, 25, 50, 100];
	private const int MaxPages = 10;

	public Task<PagedResultDto<LeaveTypeDto>?> GetActiveAsync(int skipCount, int maxResultCount) =>
		activeCache.GetAsync(GetActiveCacheKey(skipCount, maxResultCount));

	public Task SetActiveAsync(int skipCount, int maxResultCount, PagedResultDto<LeaveTypeDto> result) =>
		activeCache.SetAsync(
			GetActiveCacheKey(skipCount, maxResultCount),
			result,
			new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) });

	public Task<LeaveTypeListResponse?> GetContractAsync(
		Guid academicYearId,
		int page,
		int pageSize,
		string? search,
		CancellationToken cancellationToken) =>
		contractCache.GetAsync(GetContractCacheKey(academicYearId, page, pageSize, search), token: cancellationToken);

	public Task SetContractAsync(
		Guid academicYearId,
		int page,
		int pageSize,
		string? search,
		LeaveTypeListResponse response,
		CancellationToken cancellationToken) =>
		contractCache.SetAsync(
			GetContractCacheKey(academicYearId, page, pageSize, search),
			response,
			new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) },
			token: cancellationToken);

	public Task ClearActiveAsync()
	{
		var cacheKeysToRemove = new List<string>();
		foreach (var pageSize in CommonPageSizes)
		{
			for (int page = 0; page < MaxPages; page++)
			{
				cacheKeysToRemove.Add(GetActiveCacheKey(page * pageSize, pageSize));
			}
		}

		return activeCache.RemoveManyAsync(cacheKeysToRemove);
	}

	public Task ClearContractAsync(Guid academicYearId)
	{
		var cacheKeysToRemove = new List<string>();
		foreach (var pageSize in CommonPageSizes)
		{
			for (int page = 1; page <= MaxPages; page++)
			{
				cacheKeysToRemove.Add(GetContractCacheKey(academicYearId, page, pageSize, null));
			}
		}

		return contractCache.RemoveManyAsync(cacheKeysToRemove);
	}

	private string GetBaseCacheKey() =>
		$"EmployeeLeaveTypes:Active:Tenant:{currentTenant.Id ?? Guid.Empty}";

	private string GetActiveCacheKey(int skipCount, int maxResultCount) =>
		$"{GetBaseCacheKey()}:Skip:{skipCount}:Take:{maxResultCount}";

	private string GetContractCacheKey(Guid academicYearId, int page, int pageSize, string? search) =>
		$"EmployeeLeaveTypes:Contract:Tenant:{currentTenant.Id ?? Guid.Empty}:AcademicYear:{academicYearId}:Page:{page}:Size:{pageSize}:Search:{search ?? string.Empty}";
}
