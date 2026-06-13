using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

[RemoteService(false)]
public class LeavePolicyCacheService(
	IDistributedCache<PagedResultDto<LeavePolicyDto>> cache,
	IDistributedCache<LeavePolicyListResponse> contractCache)
	: ApplicationService, ITransientDependency
{
	public string GetActiveCacheKey(int skipCount, int maxResultCount) =>
		$"{GetBaseCacheKey()}:Skip:{skipCount}:Take:{maxResultCount}";

	public string GetContractCacheKey(Guid academicYearId, int page, int pageSize, string? search) =>
		$"EmployeeLeavePolicies:Contract:Tenant:{CurrentTenant?.Id ?? Guid.Empty}:AcademicYear:{academicYearId}:Page:{page}:Size:{pageSize}:Search:{search ?? string.Empty}";

	public Task<PagedResultDto<LeavePolicyDto>?> GetActiveAsync(string key) =>
		cache.GetAsync(key);

	public Task SetActiveAsync(string key, PagedResultDto<LeavePolicyDto> value) =>
		cache.SetAsync(
			key,
			value,
			new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) });

	public Task<LeavePolicyListResponse?> GetContractAsync(string key, System.Threading.CancellationToken cancellationToken) =>
		contractCache.GetAsync(key, token: cancellationToken);

	public Task SetContractAsync(string key, LeavePolicyListResponse value, System.Threading.CancellationToken cancellationToken) =>
		contractCache.SetAsync(
			key,
			value,
			new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) },
			token: cancellationToken);

	public Task ClearActiveAsync()
	{
		var cacheKeysToRemove = new List<string>();
		int[] commonPageSizes = { 10, 25, 50, 100, 500 };
		const int maxPages = 10;

		foreach (var pageSize in commonPageSizes)
		{
			for (var page = 0; page < maxPages; page++)
			{
				cacheKeysToRemove.Add(GetActiveCacheKey(page * pageSize, pageSize));
			}
		}

		return cache.RemoveManyAsync(cacheKeysToRemove);
	}

	public Task ClearContractAsync(Guid academicYearId)
	{
		var cacheKeysToRemove = new List<string>();
		int[] commonPageSizes = { 10, 25, 50, 100, 500 };
		const int maxPages = 10;

		foreach (var pageSize in commonPageSizes)
		{
			for (var page = 1; page <= maxPages; page++)
			{
				cacheKeysToRemove.Add(GetContractCacheKey(academicYearId, page, pageSize, null));
			}
		}

		return contractCache.RemoveManyAsync(cacheKeysToRemove);
	}

	private string GetBaseCacheKey() =>
		$"EmployeeLeavePolicies:Active:Tenant:{CurrentTenant?.Id ?? Guid.Empty}";
}
