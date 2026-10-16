using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace ShikshaMitra.HR.Seeders;

public class SecurityReleaseWorkflow_Tests
{
	[Fact]
	public void Deployment_Requires_Security_Gates_Before_Migration_And_Seeding()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null && !File.Exists(Path.Combine(directory.FullName, ".github", "workflows", "deploy.yml")))
			directory = directory.Parent;
		Assert.NotNull(directory);
		var workflow = File.ReadAllText(Path.Combine(directory.FullName, ".github", "workflows", "deploy.yml"));
		var security = File.ReadAllText(Path.Combine(directory.FullName, ".github", "workflows", "security-regression.yml"));
		Assert.Matches(@"security:\s*if: github\.ref == 'refs/heads/main'\s*uses: \./\.github/workflows/security-regression\.yml", workflow);
		Assert.Matches(@"build-and-deploy:\s*if: github\.ref == 'refs/heads/main'\s*needs: security", workflow);
		Assert.Contains("workflow_call:", security);
		Assert.Matches(@"security-backend:\s*if: github\.ref == 'refs/heads/main'", security);
		using var globalJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory.FullName, "global.json")));
		var sdk = globalJson.RootElement.GetProperty("sdk");
		Assert.Equal("9.0.100", sdk.GetProperty("version").GetString());
		Assert.Equal("latestFeature", sdk.GetProperty("rollForward").GetString());
		Assert.False(sdk.GetProperty("allowPrerelease").GetBoolean());
		Assert.Contains("[[ \"$sdk_version\" == 9.0.* ]]", security);
		Assert.Contains("[[ \"$sdk_version\" == 9.0.* ]]", workflow);
		Assert.Contains("--filter \"FullyQualifiedName~CanonicalStudentFeeAssignmentPostgres_Tests|FullyQualifiedName~LegacyMigrationBaselineBridge_Tests\"", security);
		Assert.Contains("--filter \"FullyQualifiedName~EsiFullDemoConfigurationSeederContributor_Integration_Tests\"", security);
		Assert.Contains("ShikshaMitra__Seeders__Profile: FullDemo", workflow);
		Assert.Contains("ShikshaMitra__Seeders__Enabled: 'false'", workflow);
		Assert.Contains("SeedTestLogins: 'false'", workflow);
		Assert.Contains("cancel-in-progress: false", workflow);
		Assert.Contains("- name: Run deployment startup regression gates", workflow);
		Assert.Contains("- name: Validate Azure publish profile", workflow);
		Assert.Contains("- name: Run DbMigrator", workflow);
		Assert.Contains("- name: Deploy to Azure Web App", workflow);
		var migration = workflow.IndexOf("- name: Run DbMigrator", StringComparison.Ordinal);
		Assert.True(workflow.IndexOf("- name: Run deployment startup regression gates", StringComparison.Ordinal) < migration);
		Assert.True(workflow.IndexOf("- name: Validate Azure publish profile", StringComparison.Ordinal) < migration);
		Assert.True(migration < workflow.IndexOf("- name: Deploy to Azure Web App", StringComparison.Ordinal));
	}
}
