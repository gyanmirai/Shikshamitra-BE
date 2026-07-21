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
	public void Functional_Configuration_Migration_Should_Backfill_Stable_Pipeline_Stage_Keys()
	{
		var repoRoot = FindRepositoryRoot();
		var migrationPath = Path.Combine(
			repoRoot,
			"src",
			"ShikshaMitra.EntityFrameworkCore",
			"Migrations",
			"20260720230000_AddAdmissionCrmFunctionalConfiguration.cs");
		var text = File.ReadAllText(migrationPath);

		text.ShouldContain("CASE \"\"SystemStage\"\"");
		text.ShouldContain("WHEN 0 THEN 'NewInquiry'");
		text.ShouldContain("WHEN 1 THEN 'FollowUp'");
		text.ShouldContain("WHEN 2 THEN 'ExamReady'");
		text.ShouldContain("WHEN 3 THEN 'ExamScheduled'");
		text.ShouldContain("WHEN 4 THEN 'ExamCompleted'");
		text.ShouldContain("WHEN 5 THEN 'Eligible'");
		text.ShouldContain("WHEN 6 THEN 'Waitlisted'");
		text.ShouldContain("WHEN 7 THEN 'Rejected'");
		text.ShouldContain("WHEN 8 THEN 'Stale'");
		text.ShouldContain("WHEN 9 THEN 'Archived'");
		text.ShouldNotContain("SET \"\"StageKey\"\" = \"\"SystemStage\"\"::text");
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
