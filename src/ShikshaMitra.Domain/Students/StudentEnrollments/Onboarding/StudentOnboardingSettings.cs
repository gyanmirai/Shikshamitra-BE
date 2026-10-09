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
		UpdateConfiguration(schemaVersion, configurationJson, true);
	}

	public static StudentOnboardingSettings CreateAcademicYearOverride(Guid id, StudentOnboardingSettings schoolSettings,
        Guid academicYearId, int schemaVersion, string configurationJson)
    {
        if (schoolSettings.AcademicYearId.HasValue || academicYearId == Guid.Empty)
            throw OnboardingRuleConfiguration.Invalid("Only persisted school settings can be inherited by an academic year override.");
        var settings = new StudentOnboardingSettings(id, schoolSettings.TenantId, academicYearId, schemaVersion, "{}");
        settings.ConfigurationJson = schoolSettings.ConfigurationJson;
        settings.Update(schemaVersion, configurationJson);
        return settings;
    }

	public void Update(int schemaVersion, string configurationJson) => UpdateConfiguration(schemaVersion, configurationJson, false);

	private void UpdateConfiguration(int schemaVersion, string configurationJson, bool allowLegacyInitialization)
	{
		if (schemaVersion != StudentOnboardingSettingsConsts.CurrentSchemaVersion)
			throw new ArgumentOutOfRangeException(nameof(schemaVersion));
		SchemaVersion = schemaVersion;
		ValidatePublishedVersions(ConfigurationJson, configurationJson, allowLegacyInitialization);
		ConfigurationJson = Check.NotNullOrWhiteSpace(configurationJson,
			nameof(configurationJson), StudentOnboardingSettingsConsts.MaxConfigurationLength).Trim();
	}

	private static void ValidatePublishedVersions(string previousJson, string nextJson, bool allowLegacyInitialization)
	{
		using var previous = System.Text.Json.JsonDocument.Parse(previousJson);
		using var next = System.Text.Json.JsonDocument.Parse(nextJson);
		foreach (var section in new[] { "grNumber", "rollNumber" })
		{
			if (!next.RootElement.TryGetProperty(section, out var nextSection) || !nextSection.TryGetProperty("configurations", out var nextRows))
            {
                if (previous.RootElement.TryGetProperty(section, out var existingSection) && existingSection.TryGetProperty("configurations", out var existingRows) &&
                    System.Linq.Enumerable.Any(existingRows.EnumerateArray(), r => OnboardingRuleConfiguration.IsPublished(r) || OnboardingRuleConfiguration.Text(r, "status") == "superseded"))
                    throw OnboardingRuleConfiguration.Invalid("Published numbering history cannot be removed.");
                continue;
            }
			var oldRows = previous.RootElement.TryGetProperty(section, out var oldSection) && oldSection.TryGetProperty("configurations", out var oldConfigurations)
				? System.Linq.Enumerable.ToArray(oldConfigurations.EnumerateArray()) : [];
			var newRows = System.Linq.Enumerable.ToArray(nextRows.EnumerateArray());
			foreach (var newRule in newRows)
			{
				var id = OnboardingRuleConfiguration.Text(newRule, "id");
				var oldRule = System.Linq.Enumerable.FirstOrDefault(oldRows, r => OnboardingRuleConfiguration.Text(r, "id") == id);
				var status = OnboardingRuleConfiguration.Text(newRule, "status");
				if (oldRule.ValueKind != System.Text.Json.JsonValueKind.Object && status != "draft" &&
					!(allowLegacyInitialization && string.IsNullOrWhiteSpace(status)))
					throw OnboardingRuleConfiguration.Invalid("Create and save a draft before publishing a numbering rule.");
				if (status == "published" && OnboardingRuleConfiguration.Text(oldRule, "status") == "draft")
				{
					var predecessorId = OnboardingRuleConfiguration.Text(newRule, "previousVersionId");
					if (!string.IsNullOrWhiteSpace(predecessorId))
					{
						var predecessor = System.Linq.Enumerable.FirstOrDefault(oldRows, r => OnboardingRuleConfiguration.Text(r, "id") == predecessorId);
						if (predecessor.ValueKind != System.Text.Json.JsonValueKind.Object || !OnboardingRuleConfiguration.IsPublished(predecessor))
							throw OnboardingRuleConfiguration.Invalid("This draft is based on an older superseded version. Create a draft from the current published rule.");
					}
				}
			}
			foreach (var oldRule in oldRows)
			{
				var oldStatus = OnboardingRuleConfiguration.Text(oldRule, "status");
                var legacyPublished = string.IsNullOrWhiteSpace(oldStatus) && OnboardingRuleConfiguration.IsPublished(oldRule);
                if (legacyPublished) oldStatus = "published";
				if (oldStatus is not ("published" or "superseded")) continue;
				var id = OnboardingRuleConfiguration.Text(oldRule, "id");
				var newRule = System.Linq.Enumerable.FirstOrDefault(newRows, r => OnboardingRuleConfiguration.Text(r, "id") == id);
				if (newRule.ValueKind != System.Text.Json.JsonValueKind.Object) throw OnboardingRuleConfiguration.Invalid("Published numbering history cannot be removed.");
				var before = System.Text.Json.Nodes.JsonNode.Parse(oldRule.GetRawText())!.AsObject();
				var after = System.Text.Json.Nodes.JsonNode.Parse(newRule.GetRawText())!.AsObject();
				before.Remove("status"); before.Remove("isActive"); after.Remove("status"); after.Remove("isActive");
				if (!System.Text.Json.Nodes.JsonNode.DeepEquals(before, after)) throw OnboardingRuleConfiguration.Invalid("Create a new draft version to edit numbering history.");
				var status = OnboardingRuleConfiguration.Text(newRule, "status");
                if (legacyPublished && string.IsNullOrWhiteSpace(status) && OnboardingRuleConfiguration.IsPublished(newRule)) status = "published";
				if (oldStatus == "superseded" && status != "superseded" || oldStatus == "published" &&
					(status is not ("published" or "superseded") || status == "superseded" && !System.Linq.Enumerable.Any(newRows, r => OnboardingRuleConfiguration.IsPublished(r) && OnboardingRuleConfiguration.Text(r, "previousVersionId") == id)))
					throw OnboardingRuleConfiguration.Invalid("A published rule is superseded only when its next version is published.");
			}
		}
	}

	public bool MatchesConfiguration(int schemaVersion, string configurationJson) =>
		SchemaVersion == schemaVersion &&
		string.Equals(ConfigurationJson, configurationJson.Trim(), StringComparison.Ordinal);
}
