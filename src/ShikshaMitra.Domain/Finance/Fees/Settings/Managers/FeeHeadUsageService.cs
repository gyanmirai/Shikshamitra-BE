using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Entities;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;
using Volo.Abp;

namespace ShikshaMitra.Domain.Finance.Fees.Settings.Managers;

public class FeeHeadUsageService(
	IRepository<FeeStructureDetail, Guid> feeStructureDetailRepository,
	IRepository<DueFee, Guid> dueFeeRepository,
	IRepository<FeeCollectionRecord, Guid> feeCollectionRecordRepository,
	IRepository<ConcessionFeeHeadMapping, Guid> concessionMappingRepository,
	IRepository<ConcessionDueFeeAllocation, Guid> concessionAllocationRepository,
	IRepository<FeeAdjustmentSourceLine, Guid> adjustmentSourceRepository,
	IRepository<FeeAdjustmentTargetAllocation, Guid> adjustmentTargetRepository,
	IRepository<CollectionPolicy, Guid> collectionPolicyRepository,
	IDataFilter softDeleteFilter
) : DomainService
{
	public async Task<Dictionary<Guid, List<string>>> GetUsageAsync(IEnumerable<Guid> ids)
	{
		var targets = ids.Distinct().ToHashSet();
		var result = targets.ToDictionary(id => id, _ => new List<string>());
		if (targets.Count == 0) return result;

		// A financial configuration is deletable only when it has never been referenced.
		// Include soft-deleted records because they are still part of the audit history.
		using (softDeleteFilter.Disable<ISoftDelete>())
		{
			var feeStructureRows = await feeStructureDetailRepository.GetListAsync(x => targets.Contains(x.FeeHeadId));
			AddUsage(feeStructureRows.Select(x => (x.FeeHeadId, x.Id)), result, "Fee structure");

			var dueRows = await dueFeeRepository.GetListAsync(x => targets.Contains(x.FeeHeadId));
			AddUsage(dueRows.Select(x => (x.FeeHeadId, x.Id)), result, "Student due");

			var collectionRows = await feeCollectionRecordRepository.GetListAsync(x => targets.Contains(x.FeeHeadId));
			AddUsage(collectionRows.Select(x => (x.FeeHeadId, x.Id)), result, "Fee collection");

			var concessionRows = await concessionMappingRepository.GetListAsync(x => targets.Contains(x.FeeHeadId));
			AddUsage(concessionRows.Select(x => (x.FeeHeadId, x.Id)), result, "Concession request");

			var allocationRows = await concessionAllocationRepository.GetListAsync(x => targets.Contains(x.FeeHeadId));
			AddUsage(allocationRows.Select(x => (x.FeeHeadId, x.Id)), result, "Concession allocation");

			var sourceRows = await adjustmentSourceRepository.GetListAsync(x => targets.Contains(x.FeeHeadId));
			AddUsage(sourceRows.Select(x => (x.FeeHeadId, x.Id)), result, "Fee adjustment source");

			var targetRows = await adjustmentTargetRepository.GetListAsync(x => targets.Contains(x.FeeHeadId));
			AddUsage(targetRows.Select(x => (x.FeeHeadId, x.Id)), result, "Fee adjustment target");

			var policies = await collectionPolicyRepository.GetListAsync();
			foreach (var policy in policies)
			{
				var referenced = policy.GetLateFeeApplyOnFeeHeads()
					.Concat(policy.GetWaiverApplyOnFeeHeads())
					.Concat(policy.GetFeeHeadOverrides().Select(x => x.FeeHeadId));
				foreach (var id in referenced.Where(targets.Contains).Distinct())
					result[id].Add(FormatReference("Collection policy", policy.Id));
			}
		}

		foreach (var references in result.Values) references.Sort(StringComparer.Ordinal);
		return result;
	}

	private static void AddUsage(
		IEnumerable<(Guid FeeHeadId, Guid ReferenceId)> rows,
		Dictionary<Guid, List<string>> result,
		string label)
	{
		foreach (var row in rows)
			result[row.FeeHeadId].Add(FormatReference(label, row.ReferenceId));
	}

	private static string FormatReference(string label, Guid id) => $"{label} ({id:D})";
}
