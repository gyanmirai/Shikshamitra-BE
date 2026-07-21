using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using System;
using System.Collections.Generic;
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
