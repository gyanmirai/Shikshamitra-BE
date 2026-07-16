using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPresetAdoption : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid PresetId { get; private set; }
	public int PresetVersion { get; private set; }
	public string SnapshotHash { get; private set; } = string.Empty;
	public string PreviousConfigurationJson { get; private set; } = "{}";
	public DateTime AppliedAt { get; private set; }
	public DateTime? RolledBackAt { get; private set; }

	protected AdmissionCrmPresetAdoption() { }

	public AdmissionCrmPresetAdoption(Guid id, Guid tenantId, Guid academicYearId, Guid presetId, int presetVersion, string snapshotHash, string previousConfigurationJson, DateTime appliedAt) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		PresetId = presetId;
		PresetVersion = presetVersion;
		SnapshotHash = snapshotHash;
		PreviousConfigurationJson = previousConfigurationJson;
		AppliedAt = appliedAt;
	}

	public void MarkRolledBack(DateTime now) => RolledBackAt ??= now;
}
