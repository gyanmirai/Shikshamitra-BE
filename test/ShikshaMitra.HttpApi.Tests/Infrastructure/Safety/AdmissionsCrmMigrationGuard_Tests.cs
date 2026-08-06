using System.IO;
using System.Runtime.CompilerServices;
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
