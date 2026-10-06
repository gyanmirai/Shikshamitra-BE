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
	public string NormalizedName { get; private set; } = string.Empty;
	public ICollection<FeeStructureDetail> FeeStructureDetails { get; private set; } = new List<FeeStructureDetail>();
	public bool IsLocked { get; private set; }
	public Guid RevisionRootId { get; private set; }
	public Guid? PreviousRevisionId { get; private set; }
	public int RevisionNumber { get; private set; } = 1;
	public Guid? ReceiptBookId { get; private set; }

	/// <summary>
	/// The Collection Policy attached to this Fee Structure — the resolution point
	/// CollectionPolicyResolver reads from (falls back to the academic year's
	/// isDefault Collection Policy when null). Added alongside the Collection
	/// Policy backend build; previously this attachment existed only as a
	/// frontend-only field with nothing on the backend to resolve against.
	/// </summary>
	public Guid? CollectionPolicyId { get; private set; }
	public Guid? CollectionPolicyVersionId { get; private set; }
	public Guid? ConcessionPolicyId { get; private set; }
	public Guid? ConcessionPolicyVersionId { get; private set; }
	public Guid? RefundPolicyId { get; private set; }
	public Guid? RefundPolicyVersionId { get; private set; }
	// Retired attachment storage: retained only for historical evidence and policy usage counts.
	// New structures and revisions cannot attach or inherit proration.
	public Guid? ProrationPolicyId { get; private set; }
	public Guid? ProrationPolicyVersionId { get; private set; }
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
		ChangeName(name);
		FeeStructureDetails = new List<FeeStructureDetail>();
		IsLocked = false;
		RevisionRootId = id;
	}

	public void ChangeName(string name)
	{
		EnsureNotLocked();
		var validName = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: 256).Trim();
		if (PreviousRevisionId.HasValue && Name != validName)
			throw new BusinessException(FeeStructureErrorCodes.InvalidConfiguration).WithData("Errors", new[] { "A revision retains the name of its published structure." });
		Name = validName;
		NormalizedName = Name.ToUpperInvariant();
	}

	public void SetRevision(FeeStructure previous, int revisionNumber)
	{
		EnsureNotLocked();
		if (!previous.IsLocked || revisionNumber <= previous.RevisionNumber ||
			previous.StandardId != StandardId || previous.FeeStructureMetadataId != FeeStructureMetadataId ||
			previous.TenantId != TenantId)
			throw new BusinessException(FeeStructureErrorCodes.RevisionRequiresPublication);
		RevisionRootId = previous.RevisionRootId;
		PreviousRevisionId = previous.Id;
		RevisionNumber = revisionNumber;
	}

	public void SetReceiptBook(Guid? receiptBookId)
	{
		EnsureNotLocked();
		ReceiptBookId = receiptBookId;
	}

	public void AttachCollectionPolicy(Guid? collectionPolicyId, Guid? collectionPolicyVersionId)
	{
		EnsureNotLocked();
		if (collectionPolicyId.HasValue != collectionPolicyVersionId.HasValue)
		{
			throw new BusinessException(CollectionPolicyErrorCodes.PolicyVersionNotFound);
		}
		CollectionPolicyId = collectionPolicyId;
		CollectionPolicyVersionId = collectionPolicyVersionId;
	}

	/// <summary>Legacy hydration path for rows created before immutable policy attachment.</summary>
	public void AttachCollectionPolicy(Guid? collectionPolicyId)
	{
		EnsureNotLocked();
		CollectionPolicyId = collectionPolicyId;
		CollectionPolicyVersionId = null;
	}

	public void AttachConcessionPolicy(
		Guid? concessionPolicyId,
		Guid? concessionPolicyVersionId,
		ConcessionFeeHeadScope feeHeadScope,
		IReadOnlyCollection<Guid>? applicableFeeHeadIds)
	{
		EnsureNotLocked();
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

	public void AttachRefundPolicy(Guid? refundPolicyId, Guid? refundPolicyVersionId)
	{
		EnsureNotLocked();
		if (refundPolicyId.HasValue != refundPolicyVersionId.HasValue)
			throw new BusinessException(RefundPolicyErrorCodes.PolicyVersionNotFound)
				.WithData("field", "RefundPolicyVersionId");
		RefundPolicyId = refundPolicyId;
		RefundPolicyVersionId = refundPolicyVersionId;
	}

	public void AddDetail(FeeStructureDetail detail)
	{
		EnsureNotLocked();
		if (detail.FeeStructureId != Id || detail.TenantId != TenantId || FeeStructureDetails.Any(x => x.FeeHeadId == detail.FeeHeadId))
			throw new BusinessException(FeeStructureErrorCodes.InvalidConfiguration);
		FeeStructureDetails.Add(detail);
	}

	public void ClearDetails()
	{
		EnsureNotLocked();
		FeeStructureDetails.Clear();
	}

	public void RemoveDetail(FeeStructureDetail detail)
	{
		EnsureNotLocked();
		FeeStructureDetails.Remove(detail);
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
