using ShikshaMitra.Finance.Fees.FeeCollection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.Domain.Finance.Policies;

public partial class CollectionPolicyResolver
{
	internal async Task<EffectiveCollectionRules> ResolveEffectiveRulesForAccountAsync(
		FinanceFeeAccount account, IDictionary<Guid, FinancePolicyVersion?>? versions)
	{
		if (account.StudentId.HasValue && account.StudentFeeStructureId.HasValue && account.StructureId.HasValue)
			return await ResolveEffectiveRulesForStudentAsync(account.StudentId.Value, account.StructureId.Value,
				account.StudentFeeStructureId.Value);
		var reference = account.GetPolicyReference("collection")
			?? throw new Volo.Abp.BusinessException(CollectionPolicyErrorCodes.PolicyVersionNotFound);
		FinancePolicyVersion? version;
		if (versions == null || !versions.TryGetValue(reference.VersionId, out version))
		{
			version = await versionRepository.FindAsync(reference.VersionId);
			if (versions != null) versions[reference.VersionId] = version;
		}
		if (version == null || version.PolicyType != "collection" || version.PolicyId != reference.PolicyId ||
			version.AcademicYearId != account.AcademicYearId || version.TenantId != account.TenantId)
			throw new Volo.Abp.BusinessException(CollectionPolicyErrorCodes.PolicyVersionNotFound);
		try
		{
			var snapshot = CollectionPolicySnapshot.Deserialize(version.SnapshotJson);
			var policy = CollectionPolicy.FromSnapshot(version.PolicyId, version.TenantId, version.AcademicYearId,
				version.VersionNumber, snapshot);
			return new EffectiveCollectionRules(policy, false, null, false);
		}
		catch (System.Text.Json.JsonException)
		{
			throw new Volo.Abp.BusinessException(CollectionPolicyErrorCodes.PolicyVersionNotFound);
		}
	}
}
