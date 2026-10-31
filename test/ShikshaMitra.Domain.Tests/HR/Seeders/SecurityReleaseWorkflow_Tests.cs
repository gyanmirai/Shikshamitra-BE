using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace ShikshaMitra.HR.Seeders;

public class SecurityReleaseWorkflow_Tests
{
	[Fact]
	public void Deployment_Retains_Main_Only_Release_And_Startup_Checks()
	{
		var workflow = File.ReadAllText(Path.Combine(RepositoryRoot(), ".github", "workflows", "deploy.yml"));
		// Keep the approved deployment unchanged; reusable security checks are verified separately.
		Assert.Matches(@"on:\s*push:\s*branches:\s*\['main'\]", workflow);
		Assert.Matches(@"jobs:\s*build-and-deploy:\s*if: github\.ref == 'refs/heads/main'", workflow);
		Assert.Contains("DOTNET_VERSION: '9.0.x'", workflow);
		Assert.Contains("uses: actions/setup-dotnet@v4", workflow);
		Assert.Contains("dotnet-version: ${{ env.DOTNET_VERSION }}", workflow);
		Assert.Contains("- name: Build", workflow);
		Assert.Contains("dotnet build --configuration Release", workflow);
		Assert.Contains("- name: Run deployment startup regression gates", workflow);
		Assert.Contains("--filter \"FullyQualifiedName~OpenIddictCertificateStore_Tests\"", workflow);
		Assert.Contains("- name: Validate Azure publish profile", workflow);
		Assert.Contains("- name: Run DbMigrator", workflow);
		Assert.Contains("dotnet run --configuration Release --no-launch-profile", workflow);
		Assert.Contains("- name: Deploy to Azure Web App", workflow);
		Assert.Contains("- name: Verify deployed API startup", workflow);
		var migration = workflow.IndexOf("- name: Run DbMigrator", StringComparison.Ordinal);
		var deployment = workflow.IndexOf("- name: Deploy to Azure Web App", StringComparison.Ordinal);
		Assert.True(workflow.IndexOf("- name: Build", StringComparison.Ordinal) < migration);
		Assert.True(migration < deployment);
		Assert.True(workflow.IndexOf("- name: Run deployment startup regression gates", StringComparison.Ordinal) < deployment);
		Assert.True(workflow.IndexOf("- name: Validate Azure publish profile", StringComparison.Ordinal) < deployment);
		Assert.True(deployment < workflow.IndexOf("- name: Verify deployed API startup", StringComparison.Ordinal));
	}

	[Fact]
	public void Reusable_Security_Workflow_Retains_Main_Only_Sdk_And_Regression_Checks()
	{
		var root = RepositoryRoot();
		var security = File.ReadAllText(Path.Combine(root, ".github", "workflows", "security-regression.yml"));
		Assert.Contains("workflow_call:", security);
		Assert.DoesNotContain("pull_request:", security);
		Assert.DoesNotContain("workflow_dispatch:", security);
		Assert.Matches(@"security-backend:\s*if: github\.ref == 'refs/heads/main'", security);
		using var globalJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "global.json")));
		var sdk = globalJson.RootElement.GetProperty("sdk");
		Assert.Equal("9.0.100", sdk.GetProperty("version").GetString());
		Assert.Equal("latestFeature", sdk.GetProperty("rollForward").GetString());
		Assert.False(sdk.GetProperty("allowPrerelease").GetBoolean());
		Assert.Contains("[[ \"$sdk_version\" == 9.0.* ]]", security);
		Assert.Contains("--filter \"FullyQualifiedName~CanonicalStudentFeeAssignmentPostgres_Tests|FullyQualifiedName~LegacyMigrationBaselineBridge_Tests\"", security);
		Assert.Contains("--filter \"FullyQualifiedName~EsiFullDemoConfigurationSeederContributor_Integration_Tests\"", security);
		Assert.Contains("FullyQualifiedName~UserScopeResolution_Tests", security);
		Assert.Contains("FullyQualifiedName~PermissionConsistencyApi_Tests", security);
		Assert.Contains("FullyQualifiedName~PermissionConsistencyPostgres_Tests", security);
	}

	private static string RepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null && !File.Exists(Path.Combine(directory.FullName, ".github", "workflows", "deploy.yml")))
			directory = directory.Parent;
		Assert.NotNull(directory);
		return directory.FullName;
	}
}
