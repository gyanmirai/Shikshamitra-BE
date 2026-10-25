using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.View)]
	public async Task<List<FeeStructureCopyPolicyDto>> GetCopyPoliciesAsync(Guid id, Guid feeStructureMetadataId, Guid standardId)
	{
		var source = await GetScopedAsync(id);
		var target = await EnsureStandardScopeAsync(standardId);
		var metadata = await GetMetadataReferenceAsync(feeStructureMetadataId);
		if (source.FeeStructureMetadataId != feeStructureMetadataId || metadata.AcademicYearId != target.AcademicYearId ||
			metadata.AcademicBoardId != target.EducationBoardId || metadata.AcademicMediumId != target.EducationMediumId)
			throw new BusinessException(FeeStructureErrorCodes.ScopeMismatch);
		var result = new List<FeeStructureCopyPolicyDto>();
		async Task Add(string family, Guid? policyId, Guid? sourceVersionId)
		{
			if (!policyId.HasValue) return;
			// Include historical snapshot metadata even when the master is now archived or missing.
			var snapshots = await financePolicyVersionRepository.GetListAsync(x => x.PolicyType == family &&
				x.PolicyId == policyId.Value && x.AcademicYearId == metadata.AcademicYearId);
			var original = snapshots.SingleOrDefault(x => x.Id == sourceVersionId);
			(string Name, int Version, bool Active)? master = null;
			if (family == "collection")
			{
				var policy = await collectionPolicyRepository.FindAsync(policyId.Value);
				if (policy?.AcademicYearId == metadata.AcademicYearId)
					master = (policy.PolicyName, policy.Version, policy.Status == CollectionPolicyStatus.Active);
			}
			else if (family == "refund")
			{
				var policy = await refundPolicyRepository.FindAsync(policyId.Value);
				if (policy?.AcademicYearId == metadata.AcademicYearId)
					master = (policy.PolicyName, policy.Version, policy.Status == RefundPolicyStatus.Active);
			}
			else if (family == "concession")
			{
				var policy = await concessionPolicyRepository.FindAsync(policyId.Value);
				if (policy?.AcademicYearId == metadata.AcademicYearId)
					master = (policy.PolicyName, policy.Version, policy.Status == ConcessionPolicyStatus.Active);
			}
			var current = master is { Active: true } ? snapshots.SingleOrDefault(x => x.VersionNumber == master.Value.Version && x.Status == "Active") : null;
			result.Add(new FeeStructureCopyPolicyDto
			{
				Family = family, PolicyId = policyId.Value, PolicyName = master?.Name ?? original?.PolicyName ?? policyId.Value.ToString(),
				SourceVersionId = sourceVersionId, SourceVersionNumber = original?.VersionNumber,
				CurrentVersionId = current?.Id, CurrentVersionNumber = current?.VersionNumber,
				Error = current != null ? null : master is { Active: false }
					? "This policy is inactive. Activate it before copying this structure."
					: "The current active policy snapshot is unavailable. Repair the policy and refresh before copying."
			});
		}
		await Add("collection", source.CollectionPolicyId, source.CollectionPolicyVersionId);
		await Add("concession", source.ConcessionPolicyId, source.ConcessionPolicyVersionId);
		await Add("refund", source.RefundPolicyId, source.RefundPolicyVersionId);
		return result;
	}
}
