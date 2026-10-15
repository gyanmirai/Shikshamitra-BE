using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class LeavePolicyRule : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }
	public Guid LeavePolicyId { get; protected set; }
	public LeaveAccrualPolicyType AccrualPolicyType { get; protected set; }
	public LeaveEntitlementMode EntitlementMode { get; protected set; }
	public int? LeaveCount { get; protected set; }
	public LeavePaidType PaidType { get; protected set; }
	public bool CarryForward { get; protected set; }
	public int? CarryForwardLimit { get; protected set; }
	public bool Encashable { get; protected set; }
	public LeaveEncashmentTiming? EncashmentTiming { get; protected set; }
	public bool RequiresApproval { get; protected set; }
	public int MinNoticeDays { get; protected set; }
	public int MaxDaysPerRequest { get; protected set; }
	public bool AllowHalfDay { get; protected set; }
	public int? AttachmentRequiredAfterDays { get; protected set; }
	public bool AllowNegativeBalance { get; protected set; }
	public bool IsActive { get; protected set; }
	public string PayrollCategory { get; private set; } = "PAID";

	public virtual LeavePolicy? LeavePolicy { get; protected set; }

	public bool IsUnlimited => EntitlementMode == LeaveEntitlementMode.Unlimited;

	protected LeavePolicyRule()
	{
	}

	public LeavePolicyRule(
		Guid id,
		Guid? tenantId,
		Guid leavePolicyId,
		LeavePaidType paidType,
		LeaveEntitlementMode entitlementMode,
		LeaveAccrualPolicyType accrualPolicyType,
		int? leaveCount,
		bool carryForward,
		int? carryForwardLimit,
		bool encashable,
		LeaveEncashmentTiming? encashmentTiming,
		bool requiresApproval,
		int minNoticeDays,
		int maxDaysPerRequest,
		bool allowHalfDay,
		int? attachmentRequiredAfterDays,
		bool allowNegativeBalance,
		bool isActive = true, string payrollCategory = "PAID") : base(id)
	{
		TenantId = tenantId;
		LeavePolicyId = leavePolicyId;
		PaidType = paidType;
		SetPayrollCategory(payrollCategory);
		AccrualPolicyType = accrualPolicyType;
		SetEntitlement(entitlementMode, leaveCount, allowNegativeBalance);
		SetCarryForward(carryForward, carryForwardLimit);
		SetEncashment(encashable, encashmentTiming);
		RequiresApproval = requiresApproval;
		SetRequestRules(minNoticeDays, maxDaysPerRequest, allowHalfDay, attachmentRequiredAfterDays);
		AllowNegativeBalance = allowNegativeBalance;
		IsActive = isActive;
	}

	public void Update(
		LeavePaidType paidType,
		LeaveEntitlementMode entitlementMode,
		LeaveAccrualPolicyType accrualPolicyType,
		int? leaveCount,
		bool carryForward,
		int? carryForwardLimit,
		bool encashable,
		LeaveEncashmentTiming? encashmentTiming,
		bool requiresApproval,
		int minNoticeDays,
		int maxDaysPerRequest,
		bool allowHalfDay,
		int? attachmentRequiredAfterDays,
		bool allowNegativeBalance,
		bool isActive, string payrollCategory = "PAID")
	{
		PaidType = paidType;
		SetPayrollCategory(payrollCategory);
		AccrualPolicyType = accrualPolicyType;
		SetEntitlement(entitlementMode, leaveCount, allowNegativeBalance);
		SetCarryForward(carryForward, carryForwardLimit);
		SetEncashment(encashable, encashmentTiming);
		RequiresApproval = requiresApproval;
		SetRequestRules(minNoticeDays, maxDaysPerRequest, allowHalfDay, attachmentRequiredAfterDays);
		AllowNegativeBalance = allowNegativeBalance;
		IsActive = isActive;
	}

	public void ApplyFrom(LeavePolicyRule source) =>
		Update(
			source.PaidType,
			source.EntitlementMode,
			source.AccrualPolicyType,
			source.LeaveCount,
			source.CarryForward,
			source.CarryForwardLimit,
			source.Encashable,
			source.EncashmentTiming,
			source.RequiresApproval,
			source.MinNoticeDays,
			source.MaxDaysPerRequest,
			source.AllowHalfDay,
			source.AttachmentRequiredAfterDays,
			source.AllowNegativeBalance,
			source.IsActive, source.PayrollCategory);

	public bool HasSameConfigurationAs(LeavePolicyRule other) =>
		PaidType == other.PaidType && PayrollCategory == other.PayrollCategory &&
		EntitlementMode == other.EntitlementMode &&
		AccrualPolicyType == other.AccrualPolicyType &&
		LeaveCount == other.LeaveCount &&
		CarryForward == other.CarryForward &&
		CarryForwardLimit == other.CarryForwardLimit &&
		Encashable == other.Encashable &&
		EncashmentTiming == other.EncashmentTiming &&
		RequiresApproval == other.RequiresApproval &&
		MinNoticeDays == other.MinNoticeDays &&
		MaxDaysPerRequest == other.MaxDaysPerRequest &&
		AllowHalfDay == other.AllowHalfDay &&
		AttachmentRequiredAfterDays == other.AttachmentRequiredAfterDays &&
		AllowNegativeBalance == other.AllowNegativeBalance &&
		IsActive == other.IsActive;

	public void Deactivate() => IsActive = false;

	private void SetPayrollCategory(string category)
	{
		if (category is not ("PAID" or "CL" or "UNPAID")) throw new BusinessException("HR:Leave:PayrollCategoryInvalid");
		PayrollCategory = PaidType == LeavePaidType.Unpaid ? "UNPAID" : category == "CL" ? "CL" : "PAID";
	}

	private void SetEntitlement(
		LeaveEntitlementMode entitlementMode,
		int? leaveCount,
		bool allowNegativeBalance)
	{
		if (PaidType == LeavePaidType.Paid && entitlementMode == LeaveEntitlementMode.Unlimited)
		{
			throw new BusinessException(HRErrorCodes.LeaveEntitlementRequired)
				.WithData("message", "Paid leave must have a limited entitlement.");
		}

		if (PaidType == LeavePaidType.Unpaid && entitlementMode == LeaveEntitlementMode.Unlimited)
		{
			EntitlementMode = entitlementMode;
			LeaveCount = null;
			return;
		}

		if (!leaveCount.HasValue)
		{
			throw new BusinessException(HRErrorCodes.LeaveEntitlementRequired);
		}

		if (!allowNegativeBalance &&
		    (leaveCount.Value < LeavePolicyConsts.MinLeaveCount || leaveCount.Value > LeavePolicyConsts.MaxLeaveCount))
		{
			throw new BusinessException(HRErrorCodes.LeaveInvalidCount)
				.WithData("message", $"Leave count must be between {LeavePolicyConsts.MinLeaveCount} and {LeavePolicyConsts.MaxLeaveCount}");
		}

		if (allowNegativeBalance && leaveCount.Value < 0)
		{
			throw new BusinessException(HRErrorCodes.LeaveInvalidCount);
		}

		EntitlementMode = LeaveEntitlementMode.Limited;
		LeaveCount = leaveCount.Value;
	}

	private void SetCarryForward(bool carryForward, int? carryForwardLimit)
	{
		if (PaidType == LeavePaidType.Unpaid && (carryForward || carryForwardLimit.HasValue))
		{
			throw new BusinessException(HRErrorCodes.LeaveUnpaidPaidOnlyControlsNotAllowed);
		}

		if (carryForwardLimit.HasValue && carryForwardLimit.Value < 0)
		{
			throw new BusinessException(HRErrorCodes.LeaveInvalidCount);
		}

		CarryForward = carryForward;
		CarryForwardLimit = carryForward ? carryForwardLimit : null;
	}

	private void SetEncashment(bool encashable, LeaveEncashmentTiming? encashmentTiming)
	{
		if (PaidType == LeavePaidType.Unpaid && (encashable || encashmentTiming.HasValue))
		{
			throw new BusinessException(HRErrorCodes.LeaveUnpaidPaidOnlyControlsNotAllowed);
		}

		if (encashable && !encashmentTiming.HasValue)
		{
			throw new BusinessException(HRErrorCodes.LeaveEncashmentTimingRequired);
		}

		Encashable = encashable;
		EncashmentTiming = encashable ? encashmentTiming : null;
	}

	private void SetRequestRules(
		int minNoticeDays,
		int maxDaysPerRequest,
		bool allowHalfDay,
		int? attachmentRequiredAfterDays)
	{
		if (minNoticeDays < 0 || maxDaysPerRequest <= 0 || attachmentRequiredAfterDays < 0)
		{
			throw new BusinessException(HRErrorCodes.LeaveInvalidCount);
		}

		MinNoticeDays = minNoticeDays;
		MaxDaysPerRequest = maxDaysPerRequest;
		AllowHalfDay = allowHalfDay;
		AttachmentRequiredAfterDays = attachmentRequiredAfterDays;
	}
}
