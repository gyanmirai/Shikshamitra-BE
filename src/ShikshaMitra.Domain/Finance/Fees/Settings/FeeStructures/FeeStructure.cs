using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using System;
using ShikshaMitra.Domain.Finance.Policies;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Domain.Finance.Fees.Settings;

public class FeeStructure : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	public Guid StandardId { get; private set; }
	public Guid FeeStructureMetadataId { get; private set; }

	public string Name { get; private set; }
	public ICollection<FeeStructureDetail> FeeStructureDetails { get; private set; } = new List<FeeStructureDetail>();
	public bool IsLocked { get; private set; }

	/// <summary>
	/// The Collection Policy attached to this Fee Structure — the resolution point
	/// CollectionPolicyResolver reads from (falls back to the academic year's
	/// isDefault Collection Policy when null). Added alongside the Collection
	/// Policy backend build; previously this attachment existed only as a
	/// frontend-only field with nothing on the backend to resolve against.
	/// </summary>
	public Guid? CollectionPolicyId { get; private set; }
	public Guid? ConcessionPolicyId { get; private set; }
	public Guid? ConcessionPolicyVersionId { get; private set; }
	public ConcessionFeeHeadScope ConcessionFeeHeadScope { get; private set; } = ConcessionFeeHeadScope.EntireFeeStructure;
	public string ConcessionApplicableFeeHeadIdsJson { get; private set; } = "[]";

	protected FeeStructure()
	{
	}

	public FeeStructure(
		Guid id,
		Guid? tenantId,
		Guid feeStructureMetadataId,
		Guid standardId,
		string name) : base(id)
	{
		TenantId = tenantId;
		FeeStructureMetadataId = feeStructureMetadataId;
		StandardId = standardId;
		Name = name;
		FeeStructureDetails = new List<FeeStructureDetail>();
		IsLocked = false;
	}

	public void ChangeName(string name)
	{
		Name = name;
	}

	public void AttachCollectionPolicy(Guid? collectionPolicyId)
	{
		CollectionPolicyId = collectionPolicyId;
	}

	public void AttachConcessionPolicy(
		Guid? concessionPolicyId,
		Guid? concessionPolicyVersionId,
		ConcessionFeeHeadScope feeHeadScope,
		IReadOnlyCollection<Guid>? applicableFeeHeadIds)
	{
		if (concessionPolicyId.HasValue != concessionPolicyVersionId.HasValue)
			throw new BusinessException(ConcessionPolicyErrorCodes.PolicyVersionNotFound)
				.WithData("field", "ConcessionPolicyVersionId");
		ConcessionPolicyId = concessionPolicyId;
		ConcessionPolicyVersionId = concessionPolicyVersionId;
		if (!concessionPolicyId.HasValue)
		{
			ConcessionFeeHeadScope = ConcessionFeeHeadScope.EntireFeeStructure;
			ConcessionApplicableFeeHeadIdsJson = "[]";
			return;
		}

		var ids = (applicableFeeHeadIds ?? Array.Empty<Guid>()).Distinct().Order().ToList();
		if (feeHeadScope == ConcessionFeeHeadScope.SelectedFeeHeads && ids.Count == 0)
			throw new BusinessException(ConcessionPolicyErrorCodes.FeeHeadRequired);
		ConcessionFeeHeadScope = feeHeadScope;
		ConcessionApplicableFeeHeadIdsJson = JsonSerializer.Serialize(ids);
	}

	public IReadOnlyList<Guid> GetConcessionApplicableFeeHeadIds() =>
		JsonSerializer.Deserialize<List<Guid>>(ConcessionApplicableFeeHeadIdsJson) ?? [];

	public void AddDetail(FeeStructureDetail detail)
	{
		FeeStructureDetails.Add(detail);
	}

	public void ClearDetails()
	{
		FeeStructureDetails.Clear();
	}

	public void Lock()
	{
		if (IsLocked)
			throw new BusinessException(ShikshaMitraDomainErrorCodes.FeeStructureAlreadyLocked)
				.WithData("id", Id);

		IsLocked = true;
	}

	public void EnsureNotLocked()
	{
		if (IsLocked)
			throw new BusinessException(ShikshaMitraDomainErrorCodes.FeeStructureIsLocked)
				.WithData("id", Id);
	}
}
