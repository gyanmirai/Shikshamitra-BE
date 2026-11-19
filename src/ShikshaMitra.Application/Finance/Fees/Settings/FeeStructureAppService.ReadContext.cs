using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService
{
	private sealed record MappingHead(Guid Id, string Name, string CategoryName);
	private sealed record MappingPolicy(Guid Id, string PolicyType, string PolicyName, int VersionNumber);
	private sealed class MappingVersion
	{
		public Guid Id { get; set; }
		public int VersionNumber { get; set; }
		public PaymentTermVersionStatus Status { get; set; }
	}
	private sealed class MappingTerm
	{
		public Guid Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public List<MappingVersion> Versions { get; set; } = [];
	}
	private sealed record StructureMappingContext(
		Dictionary<Guid, MappingHead> Heads,
		Dictionary<Guid, MappingTerm> Terms,
		Dictionary<Guid, MappingPolicy> Policies);

	private async Task<StructureMappingContext> LoadMappingContextAsync(IEnumerable<FeeStructure> structures)
	{
		var entities = structures.ToList();
		var details = entities.SelectMany(x => x.FeeStructureDetails).ToList();
		var headIds = details.Select(x => x.FeeHeadId).Distinct().ToArray();
		var termIds = details.Where(x => x.PaymentTermId.HasValue).Select(x => x.PaymentTermId!.Value).Distinct().ToArray();
		var policyIds = entities.SelectMany(x => new[] {
			x.CollectionPolicyVersionId, x.ConcessionPolicyVersionId, x.RefundPolicyVersionId
		}).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();

		var heads = headIds.Length == 0 ? [] : await AsyncExecuter.ToListAsync(
			(await feeHeadRepository.GetQueryableAsync()).Where(x => Enumerable.Contains(headIds, x.Id))
			.Select(x => new MappingHead(x.Id, x.Name, x.FeeCategory == null ? "" : x.FeeCategory.Name)));
		var terms = termIds.Length == 0 ? [] : await AsyncExecuter.ToListAsync(
			(await paymentTermRepository.GetQueryableAsync()).Where(x => Enumerable.Contains(termIds, x.Id))
			.Select(x => new MappingTerm {
				Id = x.Id, Name = x.Name,
				Versions = x.Versions.Select(v => new MappingVersion {
					Id = v.Id, VersionNumber = v.VersionNumber, Status = v.Status
				}).ToList()
			}));
		var policies = policyIds.Length == 0 ? [] : await AsyncExecuter.ToListAsync(
			(await financePolicyVersionRepository.GetQueryableAsync()).Where(x => Enumerable.Contains(policyIds, x.Id))
			.Select(x => new MappingPolicy(x.Id, x.PolicyType, x.PolicyName, x.VersionNumber)));
		return new StructureMappingContext(heads.ToDictionary(x => x.Id), terms.ToDictionary(x => x.Id), policies.ToDictionary(x => x.Id));
	}
}
