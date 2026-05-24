using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyVersion : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }
	public string PolicyType { get; protected set; } = string.Empty;
	public Guid PolicyId { get; protected set; }
	public Guid AcademicYearId { get; protected set; }
	public int VersionNumber { get; protected set; }
	public string Name { get; protected set; } = string.Empty;
	public string Status { get; protected set; } = string.Empty;
	public DateTime? EffectiveFrom { get; protected set; }
	public DateTime? EffectiveTo { get; protected set; }
	public string SnapshotJson { get; protected set; } = "{}";
	public string ChangeAction { get; protected set; } = "changed";
	public string? ChangeReason { get; protected set; }

	protected HrPolicyVersion()
	{
	}

	public HrPolicyVersion(
		Guid id,
		Guid? tenantId,
		string policyType,
		Guid policyId,
		Guid academicYearId,
		int versionNumber,
		string name,
		string status,
		DateTime? effectiveFrom,
		DateTime? effectiveTo,
		string snapshotJson,
		string changeAction,
		string? changeReason = null) : base(id)
	{
		TenantId = tenantId;
		PolicyType = policyType;
		PolicyId = policyId;
		AcademicYearId = academicYearId;
		VersionNumber = versionNumber;
		Name = name.Trim();
		Status = status;
		EffectiveFrom = effectiveFrom?.Date;
		EffectiveTo = effectiveTo?.Date;
		SnapshotJson = string.IsNullOrWhiteSpace(snapshotJson) ? "{}" : snapshotJson;
		ChangeAction = string.IsNullOrWhiteSpace(changeAction) ? "changed" : changeAction.Trim().ToLowerInvariant();
		ChangeReason = string.IsNullOrWhiteSpace(changeReason) ? null : changeReason.Trim();
	}
}
