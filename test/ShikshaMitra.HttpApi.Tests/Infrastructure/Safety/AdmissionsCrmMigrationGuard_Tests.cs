using System.IO;
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
		var migrationPath = Path.Combine(
			repoRoot,
			"src",
			"ShikshaMitra.EntityFrameworkCore",
			"Migrations",
			"20260804061356_v1.cs");
		var text = File.ReadAllText(migrationPath);

		text.ShouldContain("StageKey = table.Column<string>");
		text.ShouldContain("columns: new[] { \"TenantId\", \"AcademicYearId\", \"StageKey\" }");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Staging_Host_Should_Apply_Migrations_Before_Accepting_Requests()
	{
		var repoRoot = FindRepositoryRoot();
		var hostRoot = Path.Combine(repoRoot, "src", "ShikshaMitra.HttpApi.Host");
		var program = File.ReadAllText(Path.Combine(hostRoot, "Program.cs"));
		using var staging = JsonDocument.Parse(
			File.ReadAllText(Path.Combine(hostRoot, "appsettings.Staging.json")));

		program.ShouldContain("DatabaseMigrations:RunOnStartup");
		program.ShouldContain("GetRequiredService<ShikshaMitraDbMigrationService>()");
		program.IndexOf("MigrateAsync()", System.StringComparison.Ordinal)
			.ShouldBeLessThan(program.IndexOf("app.RunAsync()", System.StringComparison.Ordinal));

		staging.RootElement
			.GetProperty("DatabaseMigrations")
			.GetProperty("RunOnStartup")
			.GetBoolean()
			.ShouldBeTrue();
		staging.RootElement.GetProperty("SeedTestLogins").GetBoolean().ShouldBeTrue();
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
