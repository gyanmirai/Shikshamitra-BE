using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Timing;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.FeeCollection;

namespace ShikshaMitra.Domain.Finance.Policies;

/// <summary>
/// The single resolution algorithm every consumer (collect form, parent portal, /simulate,
/// impact preview) must call — per collection-policy-implementation-plan.md Phase 1: (1) Fee
/// Structure's direct attachment → (2) the academic year's isDefault Collection Policy → (3)
/// null (no policy attached, matches collection-policy-flow.md §7.10). Academic year is
/// resolved from the Fee Structure's own metadata rather than taken as a caller-supplied
/// parameter, since FeeStructure → FeeStructureMetadata → AcademicYearId already carries it —
/// a second, independently-passed academicYearId would just be a second source of truth that
/// could disagree with the Fee Structure's actual year.
/// </summary>
public class CollectionPolicyResolver(
	IRepository<FeeStructure, Guid> feeStructureRepository,
	IRepository<FeeStructureMetadata, Guid> feeStructureMetadataRepository,
	IRepository<CollectionPolicy, Guid> collectionPolicyRepository,
	IRepository<StudentFeePolicyOverride, Guid> overrideRepository,
	IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
	IRepository<FinancePolicyVersion, Guid> versionRepository,
	IRepository<FinanceFeeAccount, Guid> accountRepository,
	IClock clock)
	: DomainService
{
	public async Task<CollectionPolicy?> ResolveAsync(Guid feeStructureId)
	{
		var feeStructure = await feeStructureRepository.FindAsync(feeStructureId);
		if (feeStructure is null)
		{
			return null;
		}

		var metadata = await feeStructureMetadataRepository.FindAsync(feeStructure.FeeStructureMetadataId);
		if (metadata?.AcademicYearId is not { } academicYearId)
		{
			return null;
		}

		if (feeStructure.CollectionPolicyId is { } attachedId)
		{
			var attached = await collectionPolicyRepository.FindAsync(attachedId);
			if (attached is not null && attached.Status == CollectionPolicyStatus.Active &&
				attached.AcademicYearId == academicYearId && IsEffective(attached, clock.Now.Date))
			{
				return attached;
			}
			return null;
		}

		var query = await collectionPolicyRepository.GetQueryableAsync();
		return query
			.Where(p => p.AcademicYearId == academicYearId
				&& p.IsDefault
				&& p.Status == CollectionPolicyStatus.Active
				&& (!p.EffectiveFrom.HasValue || p.EffectiveFrom.Value <= clock.Now.Date)
				&& (!p.EffectiveTo.HasValue || p.EffectiveTo.Value >= clock.Now.Date))
			.OrderByDescending(p => p.CreationTime)
			.FirstOrDefault();
	}

	/// <summary>
	/// Layers a StudentFeePolicyOverride (if one exists) on top of whatever ResolveAsync
	/// returns. Override composition happens in exactly this one place — no other caller
	/// should read StudentFeePolicyOverride directly.
	/// </summary>
	public async Task<EffectiveCollectionRules> ResolveEffectiveRulesForStudentAsync(Guid studentId, Guid feeStructureId, Guid? assignmentId = null)
	{
		CollectionPolicy? policy = null;
		var assignment = await studentFeeStructureRepository.FirstOrDefaultAsync(x =>
			x.StudentId == studentId && x.FeeStructureStandardId == feeStructureId &&
            (!assignmentId.HasValue || x.Id == assignmentId.Value));
        if (assignmentId.HasValue && assignment == null)
            throw new Volo.Abp.BusinessException(CollectionPolicyErrorCodes.PolicyVersionNotFound);
		if (assignment?.CollectionPolicyVersionId is { } versionId && versionId != Guid.Empty)
		{
			var version = await versionRepository.FindAsync(versionId);
			if (version is not null && version.PolicyType == "collection" &&
				version.PolicyId == assignment.CollectionPolicyId &&
				version.AcademicYearId == assignment.AcademicYearId)
			{
				try
				{
					var snapshot = CollectionPolicySnapshot.Deserialize(version.SnapshotJson);
					policy = CollectionPolicy.FromSnapshot(
						version.PolicyId, version.TenantId, version.AcademicYearId, version.VersionNumber, snapshot);
				}
				catch (System.Text.Json.JsonException)
				{
					policy = null;
				}
			}
		}
		if (assignment != null && policy == null)
            throw new Volo.Abp.BusinessException(CollectionPolicyErrorCodes.PolicyVersionNotFound);
        policy ??= await ResolveAsync(feeStructureId);

		var query = await overrideRepository.GetQueryableAsync();
		var overrideEntity = query
			.FirstOrDefault(o => o.StudentId == studentId && o.FeeStructureId == feeStructureId);

		return new EffectiveCollectionRules(
			policy,
			overrideEntity?.IsLateFeeExempt ?? false,
			overrideEntity?.CustomGracePeriodDays,
			overrideEntity?.WaiverPreApproved ?? false);
	}

	private static bool IsEffective(CollectionPolicy policy, DateTime date) =>
		(!policy.EffectiveFrom.HasValue || policy.EffectiveFrom.Value.Date <= date.Date) &&
		(!policy.EffectiveTo.HasValue || policy.EffectiveTo.Value.Date >= date.Date);

	/// <summary>Resolves the payer's pinned Finance policy before a student assignment exists.</summary>
	public async Task<EffectiveCollectionRules> ResolveEffectiveRulesForAccountAsync(Guid accountId)
	{
		var account = await accountRepository.GetAsync(accountId);
		if (account.StudentId.HasValue && account.StudentFeeStructureId.HasValue && account.StructureId.HasValue)
			return await ResolveEffectiveRulesForStudentAsync(account.StudentId.Value, account.StructureId.Value,
				account.StudentFeeStructureId.Value);
		var reference = account.GetPolicyReference("collection")
			?? throw new Volo.Abp.BusinessException(CollectionPolicyErrorCodes.PolicyVersionNotFound);
		var version = await versionRepository.FindAsync(reference.VersionId);
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

public sealed record EffectiveCollectionRules(
	CollectionPolicy? Policy,
	bool IsLateFeeExempt,
	int? CustomGracePeriodDays,
	bool WaiverPreApproved);
