using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Policies;

public enum HrPolicyAssignmentTargetType
{
	Employee = 1,
	Group = 6
}

public class HrPolicyAssignment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }
	public Guid AcademicYearId { get; protected set; }
	public HrPolicyAssignmentTargetType TargetType { get; protected set; }
	public string TargetKey { get; protected set; } = string.Empty;
	public string? TargetDisplayName { get; protected set; }
	public Guid AttendancePolicyId { get; protected set; }
	public Guid PayrollPolicyId { get; protected set; }
	public DateTime EffectiveFrom { get; protected set; }
	public DateTime? EffectiveTo { get; protected set; }
	public bool IsActive { get; protected set; }
	public int Priority { get; protected set; }
	public string? AssignmentReason { get; protected set; }

	protected HrPolicyAssignment()
	{
	}

	public HrPolicyAssignment(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		HrPolicyAssignmentTargetType targetType,
		string targetKey,
		string? targetDisplayName,
		Guid attendancePolicyId,
		Guid payrollPolicyId,
		DateTime effectiveFrom,
		DateTime? effectiveTo,
		bool isActive,
		int priority = 0,
		string? assignmentReason = null) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		UpdateTarget(targetType, targetKey, targetDisplayName);
		Update(attendancePolicyId, payrollPolicyId, effectiveFrom, effectiveTo, isActive, priority, assignmentReason);
	}

	public void UpdateTarget(HrPolicyAssignmentTargetType targetType, string targetKey, string? targetDisplayName = null)
	{
		TargetType = targetType;
		TargetKey = NormalizeTargetKey(targetType, targetKey);
		TargetDisplayName = string.IsNullOrWhiteSpace(targetDisplayName) ? null : targetDisplayName.Trim();
	}

	public void Update(
		Guid attendancePolicyId,
		Guid payrollPolicyId,
		DateTime effectiveFrom,
		DateTime? effectiveTo,
		bool isActive,
		int priority = 0,
		string? assignmentReason = null)
	{
		if (effectiveTo.HasValue && effectiveTo.Value.Date < effectiveFrom.Date)
		{
			throw new BusinessException("ShikshaMitra:PolicyAssignmentRangeInvalid");
		}

		AttendancePolicyId = attendancePolicyId;
		PayrollPolicyId = payrollPolicyId;
		EffectiveFrom = effectiveFrom.Date;
		EffectiveTo = effectiveTo?.Date;
		IsActive = isActive;
		Priority = priority;
		AssignmentReason = string.IsNullOrWhiteSpace(assignmentReason) ? null : assignmentReason.Trim();
	}

	public bool IsEffectiveOn(DateTime date)
	{
		var candidate = date.Date;
		return IsActive && EffectiveFrom.Date <= candidate && (!EffectiveTo.HasValue || EffectiveTo.Value.Date >= candidate);
	}

	public static string NormalizeTargetKey(HrPolicyAssignmentTargetType targetType, string targetKey)
	{
		if (string.IsNullOrWhiteSpace(targetKey))
		{
			throw new BusinessException("ShikshaMitra:PolicyAssignmentTargetRequired");
		}

		var value = targetKey.Trim();
		return value.ToLowerInvariant();
	}
}
