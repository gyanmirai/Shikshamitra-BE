using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Infrastructure.Safety;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class AdmissionsCrmMigrationGuard_Tests
{
	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Baseline_Migration_Should_Keep_Tenant_Scoped_Pipeline_Stage_Keys()
	{
		var repoRoot = FindRepositoryRoot();
		var migrationDirectory = Path.Combine(repoRoot, "src", "ShikshaMitra.EntityFrameworkCore", "Migrations");
		var migrationFiles = Directory.GetFiles(migrationDirectory, "*.cs")
			.Where(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal))
			.ToArray();
		migrationFiles.ShouldNotBeEmpty();
		var text = string.Join(Environment.NewLine, migrationFiles.Select(File.ReadAllText));

		text.ShouldContain("StageKey = table.Column<string>");
		text.ShouldContain("columns: new[] { \"TenantId\", \"AcademicYearId\", \"StageKey\" }");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Staging_Host_Should_Apply_Migrations_Before_Accepting_Requests()
	{
		var repoRoot = FindRepositoryRoot();
		var hostRoot = Path.Combine(repoRoot, "src", "ShikshaMitra.HttpApi.Host");
		var hostModule = File.ReadAllText(Path.Combine(hostRoot, "ShikshaMitraHttpApiHostModule.cs"));
		var migrationWorker = File.ReadAllText(Path.Combine(
			hostRoot,
			"StagingDatabaseMigrationHostedService.cs"));
		var seederOrchestrator = File.ReadAllText(Path.Combine(
			repoRoot,
			"src",
			"ShikshaMitra.Domain",
			"Seeders",
			"ShikshaMitraSeederContributor.cs"));
		using var staging = JsonDocument.Parse(
			File.ReadAllText(Path.Combine(hostRoot, "appsettings.Staging.json")));

		hostModule.ShouldContain("AddHostedService<StagingDatabaseMigrationHostedService>()");
		migrationWorker.ShouldContain("DatabaseMigrations:RunOnStartup");
		migrationWorker.ShouldContain("BackgroundService");
		migrationWorker.ShouldContain("GetRequiredService<ShikshaMitraDbMigrationService>()");
		migrationWorker.ShouldContain("MigrateAsync()");

		staging.RootElement
			.GetProperty("DatabaseMigrations")
			.GetProperty("RunOnStartup")
			.GetBoolean()
			.ShouldBeTrue();
		staging.RootElement.GetProperty("SeedTestLogins").GetBoolean().ShouldBeTrue();
		staging.RootElement
			.GetProperty("ShikshaMitra")
			.GetProperty("Seeders")
			.GetProperty("Enabled")
			.GetBoolean()
			.ShouldBeFalse("routine stage startup must apply migrations without rerunning demo seeders");
		seederOrchestrator.ShouldContain("TestLoginSeeder:v8");
		seederOrchestrator.ShouldContain("EmployeeAcademicPermissionSeeder:v5");
		seederOrchestrator.ShouldContain("AcademicPermissionUserScopeSeeder:v9");
	}

	private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
	{
		var directory = new DirectoryInfo(Path.GetDirectoryName(sourceFilePath)!);
		while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ShikshaMitra.sln")))
		{
			directory = directory.Parent;
		}

		return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
	}
}
