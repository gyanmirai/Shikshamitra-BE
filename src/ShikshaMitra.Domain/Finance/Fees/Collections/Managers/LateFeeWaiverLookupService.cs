using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

/// <summary>
/// Sums Applied LateFeeWaiverApplication rows per due line — used by every consumer that needs
/// to net a waiver out of a computed late fee (collect-form display, collect-endpoint amount
/// validation, and the waiver application flow itself). One implementation, three callers,
/// per the "no new duplication" principle — this used to be copy-pasted three times.
/// </summary>
public class LateFeeWaiverLookupService(IRepository<LateFeeWaiverApplication, Guid> waiverRepository) : DomainService
{
	public async Task<decimal> GetAppliedWaivedAmountAsync(Guid dueFeeId)
	{
		var query = await waiverRepository.GetQueryableAsync();
		return query
			.Where(w => w.DueFeeId == dueFeeId && w.Status == LateFeeWaiverStatus.Applied)
			.Sum(w => (decimal?)w.EffectiveAmount) ?? 0m;
	}

	public async Task<Dictionary<Guid, decimal>> GetAppliedWaivedAmountsAsync(IReadOnlyList<Guid> dueFeeIds)
	{
		if (dueFeeIds.Count == 0)
		{
			return new Dictionary<Guid, decimal>();
		}

		var idList = dueFeeIds.ToList();
		var query = await waiverRepository.GetQueryableAsync();
		return query
			.Where(w => w.Status == LateFeeWaiverStatus.Applied && idList.Contains(w.DueFeeId))
			.GroupBy(w => w.DueFeeId)
			.Select(g => new { DueFeeId = g.Key, Total = g.Sum(w => w.EffectiveAmount) })
			.ToList()
			.ToDictionary(x => x.DueFeeId, x => x.Total);
	}
}
