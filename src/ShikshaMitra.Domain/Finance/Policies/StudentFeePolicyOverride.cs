using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Domain.Finance.Policies;

/// <summary>
/// Per-student adjustment layered on top of the resolved Collection Policy —
/// deliberately scoped to a small flag set, not a full policy clone, per
/// collection-policy-implementation-plan.md §0.1 decision 6.
/// </summary>
public class StudentFeePolicyOverride : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }
	public Guid StudentId { get; protected set; }
	public Guid FeeStructureId { get; protected set; }

	public bool IsLateFeeExempt { get; protected set; }
	public int? CustomGracePeriodDays { get; protected set; }
	public bool WaiverPreApproved { get; protected set; }
	public string? WaiverPreApprovalReason { get; protected set; }

	protected StudentFeePolicyOverride()
	{
	}

	public StudentFeePolicyOverride(
		Guid id,
		Guid? tenantId,
		Guid studentId,
		Guid feeStructureId) : base(id)
	{
		TenantId = tenantId;
		StudentId = studentId;
		FeeStructureId = feeStructureId;
	}

	public void Update(
		bool isLateFeeExempt,
		int? customGracePeriodDays,
		bool waiverPreApproved,
		string? waiverPreApprovalReason)
	{
		if (waiverPreApproved && string.IsNullOrWhiteSpace(waiverPreApprovalReason))
		{
			throw new BusinessException(CollectionPolicyErrorCodes.WaiverPreApprovalReasonRequired);
		}

		IsLateFeeExempt = isLateFeeExempt;
		CustomGracePeriodDays = customGracePeriodDays;
		WaiverPreApproved = waiverPreApproved;
		WaiverPreApprovalReason = waiverPreApproved ? waiverPreApprovalReason!.Trim() : null;
	}
}
