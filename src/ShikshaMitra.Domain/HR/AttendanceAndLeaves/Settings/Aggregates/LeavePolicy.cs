using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Policies;
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

	public string? Description { get; protected set; }
	public HrPolicyStatus Status { get; protected set; }
	public DateTime EffectiveFrom { get; protected set; }
	public DateTime? EffectiveTo { get; protected set; }
	public bool IsActive { get; protected set; }
	public bool IsDefault { get; protected set; }

	public virtual AcademicYear? AcademicYear { get; protected set; }
	public virtual ICollection<LeavePolicyRule> Rules { get; protected set; } = new List<LeavePolicyRule>();

	protected LeavePolicy()
	{
	}

	public LeavePolicy(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		string policyName,
		DateTime effectiveFrom,
		DateTime? effectiveTo,
		HrPolicyStatus status,
		bool isDefault = false,
		string? description = null) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		SetPolicyName(policyName);
		SetEffectiveRange(effectiveFrom, effectiveTo);
		Status = status;
		Description = description;
		IsActive = status == HrPolicyStatus.Active;
		SetDefault(isDefault);
	}

	public void Update(
		string policyName,
		DateTime effectiveFrom,
		DateTime? effectiveTo,
		HrPolicyStatus status,
		bool isDefault,
		string? description)
	{
		SetPolicyName(policyName);
		SetEffectiveRange(effectiveFrom, effectiveTo);
		Status = status;
		IsActive = status == HrPolicyStatus.Active;
		SetDefault(isDefault);
		Description = description;
	}

	public void Activate()
	{
		Status = HrPolicyStatus.Active;
		IsActive = true;
	}

	public void Deactivate()
	{
		Status = HrPolicyStatus.Archived;
		IsActive = false;
		IsDefault = false;
	}

	public void SetDefault(bool isDefault)
	{
		if (isDefault && Status != HrPolicyStatus.Active)
		{
			throw new BusinessException(HRErrorCodes.DefaultPolicyMustBeActive);
		}

		IsDefault = isDefault;
	}

	public void ReplaceRules(IEnumerable<LeavePolicyRule> rules)
	{
		Check.NotNull(rules, nameof(rules));
		var nextRules = rules.ToList();
		var duplicate = nextRules
			.Where(rule => rule.IsActive)
			.GroupBy(rule => rule.LeaveTypeId)
			.FirstOrDefault(group => group.Count() > 1);
		if (duplicate != null)
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyDuplicateLeaveTypeRule)
				.WithData("leaveTypeId", duplicate.Key);
		}

		Rules = nextRules;
	}

	public LeavePolicyRule? FindActiveRule(Guid leaveTypeId) =>
		Rules.FirstOrDefault(rule => rule.LeaveTypeId == leaveTypeId && rule.IsActive);

	private void SetPolicyName(string policyName)
	{
		Check.NotNullOrWhiteSpace(policyName, nameof(policyName));
		policyName = policyName.Trim();
		Check.Length(policyName, nameof(policyName), LeavePolicyConsts.MaxPolicyNameLength);
		PolicyName = policyName;
	}

	private void SetEffectiveRange(DateTime effectiveFrom, DateTime? effectiveTo)
	{
		if (effectiveFrom == default)
		{
			throw new BusinessException(HRErrorCodes.PolicyEffectiveFromRequired);
		}

		if (effectiveTo.HasValue && effectiveTo.Value.Date < effectiveFrom.Date)
		{
			throw new BusinessException(HRErrorCodes.PolicyAssignmentRangeInvalid);
		}

		EffectiveFrom = effectiveFrom.Date;
		EffectiveTo = effectiveTo?.Date;
	}
}
