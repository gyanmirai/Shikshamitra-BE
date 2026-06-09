using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class LeavePolicyRule : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }
	public Guid LeavePolicyId { get; protected set; }
	public Guid LeaveTypeId { get; protected set; }
	public LeaveAccrualPolicyType AccrualPolicyType { get; protected set; }
	public int LeaveCount { get; protected set; }
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

	public virtual LeavePolicy? LeavePolicy { get; protected set; }
	public virtual LeaveType? LeaveType { get; protected set; }

	protected LeavePolicyRule()
	{
	}

	public LeavePolicyRule(
		Guid id,
		Guid? tenantId,
		Guid leavePolicyId,
		Guid leaveTypeId,
		LeaveAccrualPolicyType accrualPolicyType,
		int leaveCount,
		LeavePaidType paidType,
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
		bool isActive = true) : base(id)
	{
		TenantId = tenantId;
		LeavePolicyId = leavePolicyId;
		LeaveTypeId = leaveTypeId;
		AccrualPolicyType = accrualPolicyType;
		SetLeaveCount(leaveCount, allowNegativeBalance);
		PaidType = paidType;
		SetCarryForward(carryForward, carryForwardLimit);
		SetEncashment(encashable, encashmentTiming);
		RequiresApproval = requiresApproval;
		SetRequestRules(minNoticeDays, maxDaysPerRequest, allowHalfDay, attachmentRequiredAfterDays);
		AllowNegativeBalance = allowNegativeBalance;
		IsActive = isActive;
	}

	public void Update(
		LeaveAccrualPolicyType accrualPolicyType,
		int leaveCount,
		LeavePaidType paidType,
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
		bool isActive)
	{
		AccrualPolicyType = accrualPolicyType;
		SetLeaveCount(leaveCount, allowNegativeBalance);
		PaidType = paidType;
		SetCarryForward(carryForward, carryForwardLimit);
		SetEncashment(encashable, encashmentTiming);
		RequiresApproval = requiresApproval;
		SetRequestRules(minNoticeDays, maxDaysPerRequest, allowHalfDay, attachmentRequiredAfterDays);
		AllowNegativeBalance = allowNegativeBalance;
		IsActive = isActive;
	}

	private void SetLeaveCount(int leaveCount, bool allowNegativeBalance)
	{
		if (!allowNegativeBalance &&
		    (leaveCount < LeavePolicyConsts.MinLeaveCount || leaveCount > LeavePolicyConsts.MaxLeaveCount))
		{
			throw new BusinessException(HRErrorCodes.LeaveInvalidCount)
				.WithData("message", $"Leave count must be between {LeavePolicyConsts.MinLeaveCount} and {LeavePolicyConsts.MaxLeaveCount}");
		}

		if (allowNegativeBalance && leaveCount < 0)
		{
			throw new BusinessException(HRErrorCodes.LeaveInvalidCount);
		}

		LeaveCount = leaveCount;
	}

	private void SetCarryForward(bool carryForward, int? carryForwardLimit)
	{
		if (carryForwardLimit.HasValue && carryForwardLimit.Value < 0)
		{
			throw new BusinessException(HRErrorCodes.LeaveInvalidCount);
		}

		CarryForward = carryForward;
		CarryForwardLimit = carryForward ? carryForwardLimit : null;
	}

	private void SetEncashment(bool encashable, LeaveEncashmentTiming? encashmentTiming)
	{
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
