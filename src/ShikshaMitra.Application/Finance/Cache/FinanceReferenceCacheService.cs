using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Cache;

/// <summary>
/// Short-lived in-process cache (5 minutes) for Finance reference data that is
/// read on every fee-collection and concession query but changes infrequently.
/// Avoids repeated DB round-trips for FeeHead names and ConcessionType names
/// when building student fee detail pages or concession application lists.
/// </summary>
[RemoteService(false)]
public class FinanceReferenceCacheService(
	IDistributedCache<Dictionary<Guid, string>> feeHeadCache,
	IDistributedCache<Dictionary<Guid, string>> concessionTypeCache,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<ConcessionType, Guid> concessionTypeRepository)
	: ApplicationService, ITransientDependency
{
	private static readonly DistributedCacheEntryOptions _ttl =
		new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };

	public async Task<Dictionary<Guid, string>> GetFeeHeadNameMapAsync(
		IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
	{
		var key = $"Finance:FeeHeadNames:Tenant:{CurrentTenant?.Id ?? Guid.Empty}";
		var cached = await feeHeadCache.GetAsync(key, token: cancellationToken);
		if (cached is not null) return cached;

		var all = await feeHeadRepository.GetListAsync(cancellationToken: cancellationToken);
		var map = all.ToDictionary(fh => fh.Id, fh => fh.Name);
		await feeHeadCache.SetAsync(key, map, _ttl, token: cancellationToken);
		return map;
	}

	public async Task<Dictionary<Guid, string>> GetConcessionTypeNameMapAsync(
		IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
	{
		var key = $"Finance:ConcessionTypeNames:Tenant:{CurrentTenant?.Id ?? Guid.Empty}";
		var cached = await concessionTypeCache.GetAsync(key, token: cancellationToken);
		if (cached is not null) return cached;

		var all = await concessionTypeRepository.GetListAsync(cancellationToken: cancellationToken);
		var map = all.ToDictionary(ct => ct.Id, ct => ct.Name);
		await concessionTypeCache.SetAsync(key, map, _ttl, token: cancellationToken);
		return map;
	}

	public Task InvalidateFeeHeadsAsync() =>
		feeHeadCache.RemoveAsync($"Finance:FeeHeadNames:Tenant:{CurrentTenant?.Id ?? Guid.Empty}");

	public Task InvalidateConcessionTypesAsync() =>
		concessionTypeCache.RemoveAsync($"Finance:ConcessionTypeNames:Tenant:{CurrentTenant?.Id ?? Guid.Empty}");
}
