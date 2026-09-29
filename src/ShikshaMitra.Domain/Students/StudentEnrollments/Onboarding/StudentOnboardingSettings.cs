using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public class StudentOnboardingSettings : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid? AcademicYearId { get; private set; }
	public int SchemaVersion { get; private set; }
	public string ConfigurationJson { get; private set; } = "{}";

	protected StudentOnboardingSettings() { }

	public StudentOnboardingSettings(Guid id, Guid? tenantId, Guid? academicYearId,
		int schemaVersion, string configurationJson) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		Update(schemaVersion, configurationJson);
	}

	public void Update(int schemaVersion, string configurationJson)
	{
		if (schemaVersion != StudentOnboardingSettingsConsts.CurrentSchemaVersion)
			throw new ArgumentOutOfRangeException(nameof(schemaVersion));
		SchemaVersion = schemaVersion;
		ConfigurationJson = Check.NotNullOrWhiteSpace(configurationJson,
			nameof(configurationJson), StudentOnboardingSettingsConsts.MaxConfigurationLength).Trim();
	}

	public bool MatchesConfiguration(int schemaVersion, string configurationJson) =>
		SchemaVersion == schemaVersion &&
		string.Equals(ConfigurationJson, configurationJson.Trim(), StringComparison.Ordinal);
}
