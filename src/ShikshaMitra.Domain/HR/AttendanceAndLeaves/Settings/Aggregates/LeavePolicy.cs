using System;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// Represents a leave policy configuration
/// </summary>
public class LeavePolicy : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }

	/// <summary>
	/// Academic year identifier this policy belongs to.
	/// </summary>
	public Guid AcademicYearId { get; protected set; }

	/// <summary>
	/// Name of the policy (e.g., "Sick Leave Policy")
	/// </summary>
	public string PolicyName { get; protected set; } = string.Empty;

	public Guid LeaveTypeId { get; protected set; }
	public LeaveAccrualPolicyType AccrualPolicyType { get; protected set; }
	public int LeaveCount { get; protected set; }
	public LeavePaidType PaidType { get; protected set; }
	public bool CarryForward { get; protected set; }
	public bool Encashable { get; protected set; }
	public LeaveEncashmentTiming? EncashmentTiming { get; protected set; }
	public bool RequiresApproval { get; protected set; }
	public string? Description { get; protected set; }
	public bool IsActive { get; protected set; }

	public virtual LeaveType? LeaveType { get; protected set; }
	public virtual AcademicYear? AcademicYear { get; protected set; }

	protected LeavePolicy()
	{
	}

	public LeavePolicy(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		string policyName,
		Guid leaveTypeId,
		LeaveAccrualPolicyType accrualPolicyType,
		int leaveCount,
		LeavePaidType paidType,
		bool carryForward,
		bool encashable,
		LeaveEncashmentTiming? encashmentTiming,
		bool requiresApproval,
		string? description = null) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		SetPolicyName(policyName);
		LeaveTypeId = leaveTypeId;
		AccrualPolicyType = accrualPolicyType;
		SetLeaveCount(leaveCount);
		PaidType = paidType;
		CarryForward = carryForward;
		SetEncashment(encashable, encashmentTiming);
		RequiresApproval = requiresApproval;
		Description = description;
		IsActive = true;
	}

	public void Update(
		string policyName,
		Guid leaveTypeId,
		LeaveAccrualPolicyType accrualPolicyType,
		int leaveCount,
		LeavePaidType paidType,
		bool carryForward,
		bool encashable,
		LeaveEncashmentTiming? encashmentTiming,
		bool requiresApproval,
		string? description)
	{
		SetPolicyName(policyName);
		LeaveTypeId = leaveTypeId;
		AccrualPolicyType = accrualPolicyType;
		SetLeaveCount(leaveCount);
		PaidType = paidType;
		CarryForward = carryForward;
		SetEncashment(encashable, encashmentTiming);
		RequiresApproval = requiresApproval;
		Description = description;
	}

	public void Activate() => IsActive = true;

	public void Deactivate() => IsActive = false;

	private void SetPolicyName(string policyName)
	{
		Check.NotNullOrWhiteSpace(policyName, nameof(policyName));
		policyName = policyName.Trim();
		Check.Length(policyName, nameof(policyName), LeavePolicyConsts.MaxPolicyNameLength);
		PolicyName = policyName;
	}

	private void SetLeaveCount(int leaveCount)
	{
		if (leaveCount < LeavePolicyConsts.MinLeaveCount || leaveCount > LeavePolicyConsts.MaxLeaveCount)
		{
			throw new BusinessException("ShikshaMitra:InvalidLeaveCount")
				.WithData("message", $"Leave count must be between {LeavePolicyConsts.MinLeaveCount} and {LeavePolicyConsts.MaxLeaveCount}");
		}

		LeaveCount = leaveCount;
	}

	private void SetEncashment(bool encashable, LeaveEncashmentTiming? encashmentTiming)
	{
		Encashable = encashable;
		EncashmentTiming = encashable ? encashmentTiming : null;
	}
}
