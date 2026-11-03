using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students.Dashboard;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Dashboard_onboarding_counts_share_an_aggregate_and_match_each_authorized_queue() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateOnboardingWorklistFixtureAsync();
			var service = GetRequiredService<IStudentOnboardingAppService>();
			var queues = new[] { "unfinished", "attention", "ready", "division", "gr-number", "roll-number", "documents", "credentials" };
			var counts = new System.Collections.Generic.Dictionary<string, long>();
			foreach (var queue in queues)
				counts[queue] = (await service.GetListAsync(new() { StandardId = fixture.Offering.StandardId, DashboardQueue = queue, MaxResultCount = 1 })).TotalCount;
			counts["unfinished"].ShouldBe(4);
			counts["division"].ShouldBe(1);
			counts["roll-number"].ShouldBe(1);
			var result = new StudentModuleDashboardSectionDto { Key = "admissions", AcademicYearId = fixture.Cases[0].AcademicYearId };
			await WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
				await GetRequiredService<StudentModuleDashboardAdminReader>().FillAsync(result,
					new() { Mode = "admin", Links = [new() { Id = "onboarding" }] },
					new(result.AcademicYearId, null, null, fixture.Offering.StandardId, null));
				result.Metrics.Single(metric => metric.Id == "setup").Value.ShouldBe((int)counts["unfinished"]);
				result.Metrics.Single(metric => metric.Id == "ready").Value.ShouldBe((int)counts["ready"]);
				result.AttentionTotalCount.ShouldBe((int)counts["attention"]);
				foreach (var task in new[] { ("division", "division"), ("gr", "gr-number"), ("roll", "roll-number"), ("documents", "documents"), ("credentials", "credentials"), ("ready", "ready") })
					result.OnboardingTasks.Single(metric => metric.Id == task.Item1).Value.ShouldBe((int)counts[task.Item2]);
				var aggregateReads = capture.ReadCommands.Count(sql => sql.Contains("AppStudentOnboardingCases", StringComparison.Ordinal) && sql.Contains("COUNT(", StringComparison.OrdinalIgnoreCase));
				Console.WriteLine($"SQLite admin onboarding dashboard: {capture.ReadCount} reads; {aggregateReads} case count reads.");
				aggregateReads.ShouldBeLessThanOrEqualTo(2, "Compatible counters should share one aggregate; credential ownership keeps its separate evaluator.");
			});
		});
}
