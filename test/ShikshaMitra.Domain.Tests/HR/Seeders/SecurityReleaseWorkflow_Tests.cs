using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace ShikshaMitra.HR.Seeders;

public class SecurityReleaseWorkflow_Tests
{
	[Fact]
	public void Deployment_Preserves_Main_Only_Release_Contract()
	{
		var root = FindRepositoryRoot();
		var workflow = ReadWorkflow(root, "deploy.yml");
		Assert.Matches(@"\A\s*push:\s*branches:\s*\[['""]main['""]\]\s*\z", ReadTopLevelBlock(workflow, "on"));
		AssertEveryJobRequiresMain(workflow);
		Assert.DoesNotContain("uses: ./.github/workflows/security-regression.yml", workflow);
		Assert.Matches(@"(?m)^        needs: build\s*$", workflow);
		var deployJob = workflow.IndexOf("\n    deploy:", StringComparison.Ordinal);
		Assert.True(deployJob > 0, "The workflow must define a separate deploy job.");
		var build = workflow[..deployJob];
		var deployment = workflow[deployJob..];
		Assert.Contains("group: deploy-backend-${{ github.ref }}", workflow);
		Assert.Contains("cancel-in-progress: false", workflow);
		Assert.Contains("[[ \"$sdk_version\" == 9.0.* ]]", workflow);
		Assert.Contains("ShikshaMitra__Seeders__Enabled: 'false'", workflow);
		Assert.Matches(@"(?m)^    build:\s*$", build);
		Assert.Contains("DOTNET_VERSION: '9.0.x'", workflow);
		Assert.Contains("dotnet-version: ${{ env.DOTNET_VERSION }}", workflow);
		Assert.Contains("ASPNETCORE_ENVIRONMENT: Staging", workflow);
		Assert.Contains("DOTNET_ENVIRONMENT: Staging", workflow);
		Assert.Contains("DOTNET_USELAUNCHSETTINGS: '0'", workflow);
		Assert.Contains("dotnet tool install -g Volo.Abp.Cli --version 9.0.0", workflow);
		Assert.Contains("abp install-libs", workflow);
		Assert.Contains("dotnet restore", workflow);
		Assert.Single(Regex.Matches(build, @"(?m)^[ \t]+(?:run:[ \t]+)?dotnet build --configuration Release"));
		Assert.DoesNotMatch(@"\bdotnet (build|restore|publish|run)\b", deployment);
		Assert.DoesNotContain("${{ secrets.", build);
		Assert.Contains("dotnet ShikshaMitra.DbMigrator.dll", deployment);
		Assert.Contains("ConnectionStrings__Default: ${{ secrets.DB_CONNECTION_STRING }}", workflow);
		Assert.Contains("--filter \"FullyQualifiedName~OpenIddictCertificateStore_Tests\"", workflow);
		Assert.Contains("dotnet publish src/ShikshaMitra.HttpApi.Host -c Release --no-build -o ./release/host", build);
		Assert.Contains("dotnet publish src/ShikshaMitra.DbMigrator -c Release --no-build -o ./release/migrator", build);
		Assert.Contains("uses: actions/upload-artifact@v4", build);
		Assert.Contains("uses: actions/download-artifact@v4", deployment);
		Assert.Contains("name: backend-release", build);
		Assert.Contains("name: backend-release", deployment);
		Assert.Contains("package: ./release/host", deployment);
		Assert.Contains("- name: Run deployment startup regression gates", workflow);
		Assert.Contains("- name: Validate Azure publish profile", workflow);
		Assert.Contains("- name: Run DbMigrator", workflow);
		Assert.Contains("- name: Deploy to Azure Web App", workflow);
		Assert.Contains("uses: azure/webapps-deploy@v3", workflow);
		Assert.Contains("publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}", workflow);
		Assert.Contains("clean: false", deployment);
		Assert.Contains("./scripts/deployment/Test-DeployedBackendFiles.ps1", deployment);
		Assert.Contains("- name: Verify deployed API startup", workflow);
		Assert.Contains("https://stage-api.gyanmirai.in/.well-known/openid-configuration", workflow);
		Assert.Contains("- name: Verify deployed Academic notification routes", workflow);
		Assert.Contains("./scripts/deployment/Test-DeployedAcademicNotificationRoutes.ps1", workflow);
		Assert.Contains("- name: Diagnose an existing production startup failure", workflow);
		Assert.Contains("- name: Collect Azure startup diagnostics", workflow);
		Assert.Contains("./scripts/deployment/Get-AzureStartupDiagnostics.ps1", workflow);
		var startupGate = workflow.IndexOf("- name: Run deployment startup regression gates", StringComparison.Ordinal);
		var upload = workflow.IndexOf("- name: Upload backend release", StringComparison.Ordinal);
		var extract = workflow.IndexOf("- name: Extract backend release", StringComparison.Ordinal);
		var publishGate = workflow.IndexOf("- name: Validate Azure publish profile", StringComparison.Ordinal);
		var migrate = workflow.IndexOf("- name: Run DbMigrator", StringComparison.Ordinal);
		var deploy = workflow.IndexOf("- name: Deploy to Azure Web App", StringComparison.Ordinal);
		var deployedFiles = workflow.IndexOf("- name: Verify deployed backend files", StringComparison.Ordinal);
		var startup = workflow.IndexOf("- name: Verify deployed API startup", StringComparison.Ordinal);
		var routes = workflow.IndexOf("- name: Verify deployed Academic notification routes", StringComparison.Ordinal);
		Assert.True(startupGate < upload && upload < deployJob && extract < migrate && publishGate < migrate && migrate < deploy,
			"Validate the build before uploading it, extract and validate the release before migration, and migrate before deployment.");
		Assert.True(deploy < deployedFiles && deployedFiles < startup && startup < routes,
			"Verify the deployed files before startup and check the Academic notification routes afterward.");
	}

	[Fact]
	public void Reusable_Security_Workflow_Preserves_Main_Only_Regression_Gates()
	{
		var root = FindRepositoryRoot();
		var security = ReadWorkflow(root, "security-regression.yml");
		Assert.Matches(@"\A\s*workflow_call:\s*\z", ReadTopLevelBlock(security, "on"));
		AssertEveryJobRequiresMain(security);
		Assert.Matches(@"(?m)^\s+security-backend:\s*$", security);
		using var globalJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "global.json")));
		var sdk = globalJson.RootElement.GetProperty("sdk");
		Assert.Equal("9.0.100", sdk.GetProperty("version").GetString());
		Assert.Equal("latestFeature", sdk.GetProperty("rollForward").GetString());
		Assert.False(sdk.GetProperty("allowPrerelease").GetBoolean());
		Assert.Contains("dotnet-version: '9.0.x'", security);
		Assert.Contains("[[ \"$sdk_version\" == 9.0.* ]]", security);
		Assert.Contains("DOTNET_GCHeapCount: '2'", security);
		Assert.Contains("dotnet build --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false test/ShikshaMitra.EntityFrameworkCore.Tests", security);
		Assert.Contains("dotnet build --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false test/ShikshaMitra.HttpApi.Tests", security);
		var testCommands = Regex.Matches(security, @"(?m)^\s+dotnet test [^\r\n]+");
		Assert.NotEmpty(testCommands);
		Assert.All(testCommands, command => Assert.Contains("--no-build", command.Value));
		Assert.Contains("--filter \"FullyQualifiedName~CanonicalStudentFeeAssignmentPostgres_Tests|FullyQualifiedName~LegacyMigrationBaselineBridge_Tests\"", security);
		Assert.Contains("--filter \"FullyQualifiedName~EsiFullDemoConfigurationSeederContributor_Integration_Tests\"", security);
		Assert.Contains("FullyQualifiedName~SecurityReleaseWorkflow_Tests", security);
		Assert.Contains("FullyQualifiedName~SecurityPermissionSeeder_Tests", security);
		Assert.Contains("FullyQualifiedName~RolesSeederContributor_Tests", security);
		Assert.Contains("FullyQualifiedName~PermissionConsistencyApi_Tests", security);
		Assert.Contains("FullyQualifiedName~PermissionConsistencyPostgres_Tests", security);
		Assert.Contains("FullyQualifiedName~Hierarchy_meets_query_and_latency_budget", security);
		Assert.Contains("image: postgres:17", security);
		Assert.Contains("SECURITY_INTEGRATION_POSTGRES:", security);
		Assert.Contains("SECURITY_TEST_POSTGRES:", security);
		Assert.Contains("FINANCE_MIGRATION_TEST_POSTGRES:", security);
	}

	private static string FindRepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null && !File.Exists(Path.Combine(directory.FullName, ".github", "workflows", "deploy.yml")))
			directory = directory.Parent;
		Assert.NotNull(directory);
		return directory.FullName;
	}

	private static string ReadWorkflow(string root, string fileName) =>
		File.ReadAllText(Path.Combine(root, ".github", "workflows", fileName));

	private static string ReadTopLevelBlock(string workflow, string key)
	{
		var block = Regex.Match(workflow, $@"(?m)^{Regex.Escape(key)}:[ \t]*\r?\n(?<body>(?:[ \t]+[^\r\n]*\r?\n|\r?\n)*)");
		Assert.True(block.Success, $"Workflow must define a top-level '{key}' mapping.");
		return block.Groups["body"].Value;
	}

	private static void AssertEveryJobRequiresMain(string workflow)
	{
		var jobs = ReadTopLevelBlock(workflow, "jobs");
		var firstJob = Regex.Match(jobs, @"(?m)^(?<indent> +)[\w-]+:[ \t]*\r?$");
		Assert.True(firstJob.Success, "Workflow must define at least one job.");
		var indentation = firstJob.Groups["indent"].Value;
		var jobBlocks = Regex.Matches(jobs,
			$@"(?m)^{indentation}(?<name>[\w-]+):[ \t]*\r?\n(?<body>(?:(?:{indentation} +)[^\r\n]*(?:\r?\n|\z)|\r?\n)*)");
		Assert.NotEmpty(jobBlocks);
		foreach (Match job in jobBlocks)
		{
			var body = job.Groups["body"].Value;
			var firstProperty = Regex.Match(body, @"(?m)^(?<indent> +)[\w-]+:");
			Assert.True(firstProperty.Success, $"Job '{job.Groups["name"].Value}' must define job properties.");
			Assert.Matches($@"(?m)^{firstProperty.Groups["indent"].Value}if: github\.ref == 'refs/heads/main'[ \t]*\r?$", body);
		}
	}
}
