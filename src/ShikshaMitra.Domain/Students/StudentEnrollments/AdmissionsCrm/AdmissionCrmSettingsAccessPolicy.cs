using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmSettingsAccessPolicy : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string SettingsKey { get; private set; } = string.Empty;
	public string? SectionKey { get; private set; }
	public AdmissionCrmSettingsAccessMode AccessMode { get; private set; }

	protected AdmissionCrmSettingsAccessPolicy() { }

	public AdmissionCrmSettingsAccessPolicy(Guid id, Guid tenantId, string settingsKey, string? sectionKey, AdmissionCrmSettingsAccessMode accessMode) : base(id)
	{
		TenantId = tenantId;
		Update(settingsKey, sectionKey, accessMode);
	}

	public void Update(string settingsKey, string? sectionKey, AdmissionCrmSettingsAccessMode accessMode)
	{
		SettingsKey = Check.NotNullOrWhiteSpace(settingsKey, nameof(settingsKey), 64);
		SectionKey = string.IsNullOrWhiteSpace(sectionKey) ? null : Check.Length(sectionKey.Trim(), nameof(sectionKey), 64);
		AccessMode = accessMode;
	}
}
