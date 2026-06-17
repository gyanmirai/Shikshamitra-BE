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
		EnsureRequiredActiveRules(nextRules);

		Rules = nextRules;
	}

	public void SyncRules(IEnumerable<LeavePolicyRule> rules, IReadOnlySet<Guid> referencedRuleIds)
	{
		Check.NotNull(rules, nameof(rules));
		var nextRules = rules.ToList();
		EnsureRequiredActiveRules(nextRules);

		var nextPaidTypes = nextRules.Select(rule => rule.PaidType).ToHashSet();
		foreach (var existing in Rules.Where(rule => rule.IsActive && !nextPaidTypes.Contains(rule.PaidType)))
		{
			existing.Deactivate();
		}

		foreach (var nextRule in nextRules)
		{
			var existing = Rules
				.Where(rule => rule.PaidType == nextRule.PaidType)
				.OrderByDescending(rule => rule.IsActive)
				.FirstOrDefault(rule => rule.IsActive || !referencedRuleIds.Contains(rule.Id));

			if (existing == null)
			{
				Rules.Add(nextRule);
				continue;
			}

			if (referencedRuleIds.Contains(existing.Id) && !existing.HasSameConfigurationAs(nextRule))
			{
				existing.Deactivate();
				Rules.Add(nextRule);
				continue;
			}

			existing.ApplyFrom(nextRule);
		}
	}

	public LeavePolicyRule? FindActiveRule(LeavePaidType paidType) =>
		Rules.FirstOrDefault(rule => rule.PaidType == paidType && rule.IsActive);

	private static void EnsureRequiredActiveRules(IEnumerable<LeavePolicyRule> rules)
	{
		var activeRules = rules.Where(rule => rule.IsActive).ToList();
		var duplicate = activeRules
			.Where(rule => rule.IsActive)
			.GroupBy(rule => rule.PaidType)
			.FirstOrDefault(group => group.Count() > 1);
		if (duplicate != null)
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyDuplicatePaidTypeRule)
				.WithData("paidType", duplicate.Key);
		}

		if (activeRules.Count != 2 ||
		    activeRules.All(rule => rule.PaidType != LeavePaidType.Paid) ||
		    activeRules.All(rule => rule.PaidType != LeavePaidType.Unpaid))
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyPaidUnpaidRulesRequired);
		}
	}

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
