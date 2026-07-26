using System;
using System.Threading;
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
	IDistributedCache<LeavePolicyListResponse> contractCache,
	IDistributedCache<string> generationCache)
	: ApplicationService, ITransientDependency
{
	private const string InitialGeneration = "initial";

	public async Task<string> GetActiveCacheKeyAsync(
		int skipCount,
		int maxResultCount,
		CancellationToken cancellationToken = default)
	{
		var generation = await GetGenerationAsync(GetActiveGenerationCacheKey(), cancellationToken);
		return $"{GetBaseCacheKey()}:Generation:{generation}:Skip:{skipCount}:Take:{maxResultCount}";
	}

	public async Task<string> GetContractCacheKeyAsync(
		Guid academicYearId,
		int page,
		int pageSize,
		string? search,
		bool? archivedOnly,
		CancellationToken cancellationToken = default)
	{
		var generation = await GetGenerationAsync(GetContractGenerationCacheKey(academicYearId), cancellationToken);
		return $"EmployeeLeavePolicies:Contract:Tenant:{CurrentTenant?.Id ?? Guid.Empty}:AcademicYear:{academicYearId}:Generation:{generation}:Page:{page}:Size:{pageSize}:Search:{search ?? string.Empty}:Archived:{archivedOnly?.ToString() ?? "ActiveOnly"}";
	}

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

	public Task ClearActiveAsync() =>
		RotateGenerationAfterCommitAsync(GetActiveGenerationCacheKey());

	public Task ClearContractAsync(Guid academicYearId) =>
		RotateGenerationAfterCommitAsync(GetContractGenerationCacheKey(academicYearId));

	private async Task<string> GetGenerationAsync(string key, CancellationToken cancellationToken)
	{
		return await generationCache.GetAsync(key, token: cancellationToken) ?? InitialGeneration;
	}

	private Task RotateGenerationAfterCommitAsync(string key)
	{
		var currentUnitOfWork = UnitOfWorkManager.Current;
		if (currentUnitOfWork is null)
		{
			return RotateGenerationAsync(key);
		}

		currentUnitOfWork.OnCompleted(() => RotateGenerationAsync(key));
		return Task.CompletedTask;
	}

	private Task RotateGenerationAsync(string key) =>
		generationCache.SetAsync(
			key,
			Guid.NewGuid().ToString("N"),
			new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(48) });

	private string GetBaseCacheKey() =>
		$"EmployeeLeavePolicies:Active:Tenant:{CurrentTenant?.Id ?? Guid.Empty}";

	private string GetActiveGenerationCacheKey() =>
		$"EmployeeLeavePolicies:ActiveGeneration:Tenant:{CurrentTenant?.Id ?? Guid.Empty}";

	private string GetContractGenerationCacheKey(Guid academicYearId) =>
		$"EmployeeLeavePolicies:ContractGeneration:Tenant:{CurrentTenant?.Id ?? Guid.Empty}:AcademicYear:{academicYearId}";
}
