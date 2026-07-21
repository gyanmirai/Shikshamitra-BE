using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Domain.Finance.Policies;

/// <summary>
/// Immutable version snapshot for any finance policy kind, discriminated by
/// <see cref="PolicyType"/> — mirrors HrPolicyVersion's single-shared-table
/// pattern (src/ShikshaMitra.Domain/HR/Policies/Aggregates/HrPolicyVersion.cs)
/// per collection-policy-implementation-plan.md §0.1 decision 5: Collection
/// Policy uses this table today ("collection"); Payment Policy (Phase 6)
/// reuses the same table with PolicyType = "payment" instead of a second one.
/// </summary>
public class FinancePolicyVersion : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }
	public string PolicyType { get; protected set; } = string.Empty;
	public Guid PolicyId { get; protected set; }
	public Guid AcademicYearId { get; protected set; }
	public int VersionNumber { get; protected set; }
	public string PolicyName { get; protected set; } = string.Empty;
	public string Status { get; protected set; } = string.Empty;
	public string SnapshotJson { get; protected set; } = "{}";
	public string ChangeAction { get; protected set; } = "changed";
	public string? ChangeReason { get; protected set; }

	protected FinancePolicyVersion()
	{
	}

	public FinancePolicyVersion(
		Guid id,
		Guid? tenantId,
		string policyType,
		Guid policyId,
		Guid academicYearId,
		int versionNumber,
		string policyName,
		string status,
		string snapshotJson,
		string changeAction,
		string? changeReason = null) : base(id)
	{
		TenantId = tenantId;
		PolicyType = policyType;
		PolicyId = policyId;
		AcademicYearId = academicYearId;
		VersionNumber = versionNumber;
		PolicyName = policyName.Trim();
		Status = status;
		SnapshotJson = string.IsNullOrWhiteSpace(snapshotJson) ? "{}" : snapshotJson;
		ChangeAction = string.IsNullOrWhiteSpace(changeAction) ? "changed" : changeAction.Trim().ToLowerInvariant();
		ChangeReason = string.IsNullOrWhiteSpace(changeReason) ? null : changeReason.Trim();
	}
}
