using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;

namespace ShikshaMitra.Domain.Finance.Policies;

public partial class CollectionPolicyResolver
{
	// Only reference rows are shared within one balance calculation. Each assignment
	// still validates its own pin and receives a separate policy aggregate.
	internal sealed record StudentPolicyReadContext(
		IReadOnlyDictionary<Guid, FinancePolicyVersion> Versions,
		ILookup<(Guid StudentId, Guid FeeStructureId), StudentFeePolicyOverride> Overrides);

	internal async Task<StudentPolicyReadContext> CreateStudentReadContextAsync(
		IReadOnlyCollection<StudentFeeStructure> assignments)
	{
		var versionIds = assignments.Where(x => x.CollectionPolicyVersionId.HasValue
			&& x.CollectionPolicyVersionId.Value != Guid.Empty)
			.Select(x => x.CollectionPolicyVersionId!.Value).Distinct().ToArray();
		var versions = versionIds.Length == 0 ? new List<FinancePolicyVersion>()
			: await versionRepository.GetListAsync(x => Enumerable.Contains(versionIds, x.Id));
		var studentIds = assignments.Select(x => x.StudentId).Distinct().ToArray();
		var structureIds = assignments.Select(x => x.FeeStructureStandardId).Distinct().ToArray();
		var overrides = studentIds.Length == 0 ? new List<StudentFeePolicyOverride>()
			: await overrideRepository.GetListAsync(x => Enumerable.Contains(studentIds, x.StudentId)
				&& Enumerable.Contains(structureIds, x.FeeStructureId));
		return new(versions.ToDictionary(x => x.Id),
			overrides.ToLookup(x => (x.StudentId, x.FeeStructureId)));
	}

	internal async Task<EffectiveCollectionRules> ResolveEffectiveRulesForStudentAsync(
		StudentFeeStructure assignment, StudentPolicyReadContext context)
	{
		FinancePolicyVersion? version = null;
		if (assignment.CollectionPolicyVersionId is { } versionId && versionId != Guid.Empty)
			context.Versions.TryGetValue(versionId, out version);
		var policy = ReadStudentPinnedPolicy(assignment, version)
			?? throw new BusinessException(CollectionPolicyErrorCodes.PolicyVersionNotFound);
		var candidates = context.Overrides[(assignment.StudentId, assignment.FeeStructureStandardId)].ToList();
		var overrideEntity = candidates.FirstOrDefault();
		if (candidates.Count > 1)
		{
			// Retain the original provider's selection for ambiguous legacy rows,
			// including callers that deliberately disable the soft-delete filter.
			var query = await overrideRepository.GetQueryableAsync();
			overrideEntity = query.FirstOrDefault(x => x.StudentId == assignment.StudentId
				&& x.FeeStructureId == assignment.FeeStructureStandardId);
		}
		return new EffectiveCollectionRules(policy, overrideEntity?.IsLateFeeExempt ?? false,
			overrideEntity?.CustomGracePeriodDays, overrideEntity?.WaiverPreApproved ?? false);
	}

	private static CollectionPolicy? ReadStudentPinnedPolicy(
		StudentFeeStructure assignment, FinancePolicyVersion? version)
	{
		if (version is null || version.PolicyType != "collection"
			|| version.PolicyId != assignment.CollectionPolicyId
			|| version.AcademicYearId != assignment.AcademicYearId) return null;
		try
		{
			var snapshot = CollectionPolicySnapshot.Deserialize(version.SnapshotJson);
			return CollectionPolicy.FromSnapshot(version.PolicyId, version.TenantId,
				version.AcademicYearId, version.VersionNumber, snapshot);
		}
		catch (System.Text.Json.JsonException) { return null; }
	}
}
