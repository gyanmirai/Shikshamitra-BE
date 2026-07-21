using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

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
	IRepository<StudentFeePolicyOverride, Guid> overrideRepository)
	: DomainService
{
	public async Task<CollectionPolicy?> ResolveAsync(Guid feeStructureId)
	{
		var feeStructure = await feeStructureRepository.FindAsync(feeStructureId);
		if (feeStructure is null)
		{
			return null;
		}

		if (feeStructure.CollectionPolicyId is { } attachedId)
		{
			var attached = await collectionPolicyRepository.FindAsync(attachedId);
			if (attached is not null)
			{
				return attached;
			}
		}

		var metadata = await feeStructureMetadataRepository.FindAsync(feeStructure.FeeStructureMetadataId);
		if (metadata?.AcademicYearId is not { } academicYearId)
		{
			return null;
		}

		var query = await collectionPolicyRepository.GetQueryableAsync();
		return query
			.Where(p => p.AcademicYearId == academicYearId
				&& p.IsDefault
				&& p.Status == CollectionPolicyStatus.Active)
			.OrderByDescending(p => p.CreationTime)
			.FirstOrDefault();
	}

	/// <summary>
	/// Layers a StudentFeePolicyOverride (if one exists) on top of whatever ResolveAsync
	/// returns. Override composition happens in exactly this one place — no other caller
	/// should read StudentFeePolicyOverride directly.
	/// </summary>
	public async Task<EffectiveCollectionRules> ResolveEffectiveRulesForStudentAsync(Guid studentId, Guid feeStructureId)
	{
		var policy = await ResolveAsync(feeStructureId);

		var query = await overrideRepository.GetQueryableAsync();
		var overrideEntity = query
			.FirstOrDefault(o => o.StudentId == studentId && o.FeeStructureId == feeStructureId);

		return new EffectiveCollectionRules(
			policy,
			overrideEntity?.IsLateFeeExempt ?? false,
			overrideEntity?.CustomGracePeriodDays,
			overrideEntity?.WaiverPreApproved ?? false);
	}
}

public sealed record EffectiveCollectionRules(
	CollectionPolicy? Policy,
	bool IsLateFeeExempt,
	int? CustomGracePeriodDays,
	bool WaiverPreApproved);
