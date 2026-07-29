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
			"20260721060936_v1.cs");
		var text = File.ReadAllText(migrationPath);

		text.ShouldContain("StageKey = table.Column<string>");
		text.ShouldContain("columns: new[] { \"TenantId\", \"AcademicYearId\", \"StageKey\" }");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Admission_Year_Setup_Migration_Should_Stay_Inside_Enrollment()
	{
		var repoRoot = FindRepositoryRoot();
		var migrationPath = Path.Combine(
			repoRoot,
			"src",
			"ShikshaMitra.EntityFrameworkCore",
			"Migrations",
			"20260729044230_AddAdmissionYearSetupVersions.cs");
		var text = File.ReadAllText(migrationPath);

		text.ShouldContain("AppAdmissionCrmAdmissionYearSetups");
		text.ShouldContain("AdmissionYearSetupSnapshotJson");
		text.ShouldContain("AdmissionYearSetupVersion");
		text.ShouldContain("\\\"Status\\\" IN (0, 1)");
		text.ShouldNotContain("AppAssessmentRubrics");
		text.ShouldNotContain("AppAcademicCalendars");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Inquiry_Type_Migration_Should_Stay_Inside_Enrollment()
	{
		var repoRoot = FindRepositoryRoot();
		var migrationPath = Path.Combine(
			repoRoot,
			"src",
			"ShikshaMitra.EntityFrameworkCore",
			"Migrations",
			"20260729063338_AddAdmissionCrmInquiryTypes.cs");
		var text = File.ReadAllText(migrationPath);

		text.ShouldContain("AppAdmissionCrmInquiryTypes");
		text.ShouldContain("InquiryTypeId");
		text.ShouldContain("InquiryTypeSnapshot");
		text.ShouldNotContain("AppAssessmentRubrics");
		text.ShouldNotContain("AppAcademicCalendars");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Timeline_Utc_Sort_Migration_Should_Backfill_And_Stay_Inside_Enrollment()
	{
		var repoRoot = FindRepositoryRoot();
		var migrationPath = Path.Combine(
			repoRoot,
			"src",
			"ShikshaMitra.EntityFrameworkCore",
			"Migrations",
			"20260729083608_AddAdmissionCrmTimelineUtcSort.cs");
		var text = File.ReadAllText(migrationPath);

		text.ShouldContain("AppAdmissionCrmTimelineEvents");
		text.ShouldContain("OccurredAtUtc");
		text.ShouldContain("SET \"OccurredAtUtc\" = \"OccurredAt\"");
		text.ShouldContain("TenantId\", \"LeadId\", \"OccurredAtUtc");
		text.ShouldNotContain("AppAssessmentRubrics");
		text.ShouldNotContain("AppAcademicCalendars");
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
