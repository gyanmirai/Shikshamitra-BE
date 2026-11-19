using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Onboarding_credentials_queue_batches_parent_flow_metadata_without_changing_filtered_totals() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateOnboardingWorklistFixtureAsync();
			async Task<int> Measure(StudentOnboardingStatus? status) => await WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
				var result = await GetRequiredService<IStudentOnboardingAppService>().GetListAsync(new()
				{
					StandardId = fixture.Offering.StandardId, PendingTask = "credentials", Status = status, MaxResultCount = 1
				});
				result.TotalCount.ShouldBe(status.HasValue ? 1 : 4, "The default required guardian account remains pending for each unenrolled case.");
				result.Items.Count.ShouldBe(1);
				return capture.ReadCommands.Count(sql => sql.Contains("FROM \"AppAdmissionApplicants\"", StringComparison.Ordinal));
			});
			var single = await Measure(StudentOnboardingStatus.Pending);
			var all = await Measure(null);
			Console.WriteLine($"SQLite credentials queue candidates=1/4; applicant reads={single}/{all}.");
			single.ShouldBe(1);
			all.ShouldBe(1, "Read each cohort's parent-flow metadata together before counting and paging.");
		});

	[Fact]
	public Task Onboarding_analytics_aggregates_scoped_counts_in_one_case_read() =>
		RunCertificationScenarioAsync(async () =>
		{
			await CreateOnboardingWorklistFixtureAsync();
			await WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
				var result = await GetRequiredService<IStudentOnboardingAppService>().GetAnalyticsAsync();
				result.ActiveCases.ShouldBe(4);
				result.Pending.ShouldBe(1);
				result.InProgress.ShouldBe(3);
				result.ReadyForActivation.ShouldBe(0);
				result.NeedsAttention.ShouldBe(0);
				result.Overdue.ShouldBe(0);
				result.Completed.ShouldBe(0);
				result.Cancelled.ShouldBe(0);
				var caseReads = capture.ReadCommands.Count(sql => sql.Contains("FROM \"AppStudentOnboardingCases\"", StringComparison.Ordinal));
				Console.WriteLine($"SQLite onboarding analytics: {capture.ReadCount} reads, {caseReads} case reads.");
				caseReads.ShouldBe(1, "Dashboard counts should share one scoped aggregate query.");
			});
			using (_currentTenant.Change(Guid.NewGuid()))
			{
				var empty = await GetRequiredService<IStudentOnboardingAppService>().GetAnalyticsAsync();
				empty.ActiveCases.ShouldBe(0);
				empty.Pending.ShouldBe(0);
				empty.InProgress.ShouldBe(0);
				empty.Overdue.ShouldBe(0);
			}
		});

	[Fact]
	public Task Onboarding_placement_choices_batch_counts_without_tracking_divisions_or_reservations() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateOnboardingWorklistFixtureAsync();
			var first = await MeasurePlacementChoicesAsync(fixture.Cases[0].Id);
			var optionA = first.Items.Single(item => item.Id == fixture.DivisionA);
			optionA.Name.ShouldBe("Worklist A");
			optionA.Capacity.ShouldBe(30);
			optionA.LiveReservations.ShouldBe(2);
			optionA.AvailableSeats.ShouldBe(28 - optionA.ActiveEnrollments);
			first.Items.Single(item => item.Id == fixture.DivisionB).LiveReservations.ShouldBe(1);
			await WithUnitOfWorkAsync(async () =>
			{
				var manager = GetRequiredService<DivisionManager>();
				var divisions = GetRequiredService<IRepository<Division, Guid>>();
				for (var index = 0; index < 4; index++)
					await divisions.InsertAsync(await manager.CreateAsync(fixture.Offering.StandardId, $"Perf extra {index}", 30), true);
				await GetRequiredService<IRepository<StudentSeatReservation, Guid>>().InsertAsync(new(
					Guid.NewGuid(), TestTenantId, fixture.Cases[3].Id, fixture.Cases[0].AcademicYearId,
					fixture.Offering.StandardId, fixture.DivisionA, 3, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1)), true);
			});
			var many = await MeasurePlacementChoicesAsync(fixture.Cases[0].Id);
			many.Items.Count.ShouldBe(first.Items.Count + 4);
			many.Items.Single(item => item.Id == fixture.DivisionA).LiveReservations.ShouldBe(2, "Expired reservations must not consume seats.");
			Console.WriteLine($"SQLite onboarding placements: {first.Items.Count} divisions={first.Reads} reads; {many.Items.Count} divisions={many.Reads} reads.");
			many.Reads.ShouldBeLessThanOrEqualTo(first.Reads, "Choice query count must stay fixed as divisions grow.");
			many.TrackedDivisions.ShouldBe(0);
			many.TrackedReservations.ShouldBe(0);
		});

	private async Task<(System.Collections.Generic.IReadOnlyList<StudentOnboardingPlacementOptionDto> Items, int Reads, int TrackedDivisions, int TrackedReservations)> MeasurePlacementChoicesAsync(Guid caseId) =>
		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			db.ChangeTracker.Clear();
			using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
			var result = await GetRequiredService<IStudentOnboardingAppService>().GetPlacementOptionsAsync(caseId);
			return (result, capture.ReadCount, db.ChangeTracker.Entries<Division>().Count(), db.ChangeTracker.Entries<StudentSeatReservation>().Count());
		});
}
