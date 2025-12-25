using System;
using ShikshaMitra.TenantSettings.EducationBoards;
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
	/// Organization/School identifier this policy belongs to
	/// </summary>
	public Guid EducationMediumStageId { get; protected set; }
	
	/// <summary>
	/// Name of the policy (e.g., "Sick Leave Policy")
	/// </summary>
	public string PolicyName { get; protected set; } = string.Empty;
	
	/// <summary>
	/// Foreign key to the associated leave type
	/// </summary>
	public Guid LeaveTypeId { get; protected set; }
	
	/// <summary>
	/// Maximum days allowed per year
	/// </summary>
	public int MaximumDaysPerYear { get; protected set; }
	
	/// <summary>
	/// Whether this is paid or unpaid leave
	/// </summary>
	public LeavePaidType PaidType { get; protected set; }
	
	/// <summary>
	/// Whether unused leave can be carried forward to next year
	/// </summary>
	public bool CarryForward { get; protected set; }
	
	/// <summary>
	/// Whether unused leave can be encashed
	/// </summary>
	public bool Encashable { get; protected set; }
	
	/// <summary>
	/// Whether leave requires approval before being granted
	/// </summary>
	public bool RequiresApproval { get; protected set; }
	
	/// <summary>
	/// Optional description/notes about the policy
	/// </summary>
	public string? Description { get; protected set; }
	
	/// <summary>
	/// Whether this policy is active
	/// </summary>
	public bool IsActive { get; protected set; }
	
	/// <summary>
	/// Navigation property for LeaveType
	/// </summary>
	public virtual LeaveType? LeaveType { get; protected set; }
	
	/// <summary>
	/// Navigation property for EducationMediumStage
	/// </summary>
	public virtual EducationMediumStage? EducationMediumStage { get; protected set; }

	// For EF Core
	protected LeavePolicy()
	{
	}

	public LeavePolicy(
		Guid id,
		Guid? tenantId,
		Guid educationMediumStageId,
		string policyName,
		Guid leaveTypeId,
		int maximumDaysPerYear,
		LeavePaidType paidType,
		bool carryForward,
		bool encashable,
		bool requiresApproval,
		string? description = null) : base(id)
	{
		TenantId = tenantId;
		EducationMediumStageId = educationMediumStageId;
		SetPolicyName(policyName);
		LeaveTypeId = leaveTypeId;
		SetMaximumDaysPerYear(maximumDaysPerYear);
		PaidType = paidType;
		CarryForward = carryForward;
		Encashable = encashable;
		RequiresApproval = requiresApproval;
		Description = description;
		IsActive = true;
	}

	public void Update(
		string policyName,
		Guid leaveTypeId,
		int maximumDaysPerYear,
		LeavePaidType paidType,
		bool carryForward,
		bool encashable,
		bool requiresApproval,
		string? description)
	{
		SetPolicyName(policyName);
		LeaveTypeId = leaveTypeId;
		SetMaximumDaysPerYear(maximumDaysPerYear);
		PaidType = paidType;
		CarryForward = carryForward;
		Encashable = encashable;
		RequiresApproval = requiresApproval;
		Description = description;
	}

	public void Activate()
	{
		IsActive = true;
	}

	public void Deactivate()
	{
		IsActive = false;
	}

	private void SetPolicyName(string policyName)
	{
		Check.NotNullOrWhiteSpace(policyName, nameof(policyName));
		Check.Length(policyName, nameof(policyName), LeavePolicyConsts.MaxPolicyNameLength);
		PolicyName = policyName;
	}

	private void SetMaximumDaysPerYear(int maximumDaysPerYear)
	{
		if (maximumDaysPerYear < LeavePolicyConsts.MinMaximumDaysPerYear || 
		    maximumDaysPerYear > LeavePolicyConsts.MaxMaximumDaysPerYear)
		{
			throw new BusinessException("ShikshaMitra:InvalidMaximumDays")
				.WithData("message", $"Maximum days per year must be between {LeavePolicyConsts.MinMaximumDaysPerYear} and {LeavePolicyConsts.MaxMaximumDaysPerYear}");
		}
		MaximumDaysPerYear = maximumDaysPerYear;
	}
}
