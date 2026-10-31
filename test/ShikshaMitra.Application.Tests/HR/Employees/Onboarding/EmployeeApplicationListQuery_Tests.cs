using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Xunit;
using Xunit.Abstractions;

namespace ShikshaMitra.HR.Employees.Onboarding;

public class EmployeeApplicationListQuery_Tests(ITestOutputHelper output)
	: ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Theory]
	[InlineData(35)]
	[InlineData(200)]
	public async Task List_projects_only_summary_columns_with_two_queries_and_no_tracked_graph(int population)
	{
		var tenantId = Guid.NewGuid();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<EmployeeApplication, Guid>>();
			for (var index = 0; index < population + 2; index++)
			{
				var item = Application(index == population ? Guid.NewGuid() : tenantId, index);
				item.IsDeleted = index == population + 1;
				await repository.InsertAsync(item, true);
			}
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			var query = await GetRequiredService<IRepository<EmployeeApplication, Guid>>().GetQueryableAsync();
			// Reproduce the previous list read for directly comparable local SQL evidence.
			using var before = new AcademicQueryCounter(db.Database.GetDbConnection());
			var baselineCount = await query.CountAsync();
			var baseline = await query.OrderByDescending(x => x.CreationTime).ThenBy(x => x.Id).Take(20).ToListAsync();
			var beforeSql = before.ReadCommands.Single(x => !x.Contains("COUNT(*)", StringComparison.Ordinal));
			var beforeCount = before.ReadCount;
			db.ChangeTracker.Clear();
			using var after = new AcademicQueryCounter(db.Database.GetDbConnection());
			var result = await GetRequiredService<IEmployeeApplicationAppService>().GetListAsync(new() { MaxResultCount = 20 });
			var applicationReads = after.ReadCommands.Where(sql => sql.Contains("\"AppEmployeeApplications\"", StringComparison.Ordinal)).ToList();
			applicationReads.Count.ShouldBe(2);
			beforeCount.ShouldBe(2);
			result.TotalCount.ShouldBe(population);
			result.TotalCount.ShouldBe(baselineCount);
			result.Items.Select(x => x.Id).ShouldBe(baseline.Select(x => x.Id));
			for (var index = 0; index < baseline.Count; index++) AssertSummary(result.Items[index], baseline[index]);
			db.ChangeTracker.Entries<EmployeeApplication>().ShouldBeEmpty();
			var afterSql = applicationReads.Single(x => !x.Contains("COUNT(*)", StringComparison.Ordinal));
			foreach (var column in new[] { "PersonalInfo", "FamilyInfo", "AdditionalInfo", "WorkExperiences", "TokenHash", "EncryptedToken", "Address1" })
			{
				beforeSql.ShouldContain(column);
				afterSql.ShouldNotContain(column);
			}
			afterSql.ShouldContain("LIMIT");
			afterSql.ShouldContain("ORDER BY");
			output.WriteLine($"Synthetic SQLite; population={population}, page=20; application queries {beforeCount}->{applicationReads.Count} (service total {after.ReadCount}, including framework reads); SQL chars {beforeSql.Length}->{afterSql.Length}; tracked application graphs {baseline.Count}->0.");
			foreach (var frameworkSql in after.ReadCommands.Except(applicationReads)) output.WriteLine("FRAMEWORK SQL: " + frameworkSql);
			output.WriteLine("BEFORE SQL: " + beforeSql);
			output.WriteLine("AFTER SQL: " + afterSql);
			output.WriteLine($"Distinct selected database columns {ColumnCount(beforeSql)}->{ColumnCount(afterSql)}.");
			// Alternating warm local materialization samples, not deployed request timings.
			var oldSamples = new List<double>();
			var newSamples = new List<double>();
			var page = query.OrderByDescending(x => x.CreationTime).ThenBy(x => x.Id).Take(20);
			for (var sample = 0; sample < 35; sample++)
			{
				db.ChangeTracker.Clear();
				var timer = Stopwatch.StartNew();
				await page.ToListAsync();
				timer.Stop();
				if (sample >= 5) oldSamples.Add(timer.Elapsed.TotalMilliseconds);
				db.ChangeTracker.Clear();
				timer.Restart();
				await page.Select(EmployeeApplicationListProjection.Item).ToListAsync();
				timer.Stop();
				if (sample >= 5) newSamples.Add(timer.Elapsed.TotalMilliseconds);
			}
			oldSamples.Sort();
			newSamples.Sort();
			output.WriteLine($"SQLite materialization only, 5 warmups + 30 samples: median {oldSamples[15]:F3}->{newSamples[15]:F3} ms; p95 {oldSamples[28]:F3}->{newSamples[28]:F3} ms. Excludes network, authentication and count query.");
		});
	}

	[Fact]
	public async Task List_preserves_search_status_paging_empty_results_and_display_contract()
	{
		using var tenant = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		var expected = new List<EmployeeApplication>();
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<EmployeeApplication, Guid>>();
			for (var index = 0; index < 5; index++)
			{
				var item = Application(GetRequiredService<ICurrentTenant>().Id!.Value, index);
				if (index == 4) item.ChangeStatus(EmployeeApplicationStatus.Rejected, "Test rejection");
				expected.Add(item);
				await repository.InsertAsync(item, true);
			}
		});
		var service = GetRequiredService<IEmployeeApplicationAppService>();
		var input = new GetEmployeeApplicationListInput { Search = "  SYNTHETIC  ", Status = EmployeeApplicationStatus.InvitationSent, SkipCount = 1, MaxResultCount = 2 };
		var page = await service.GetListAsync(input);
		page.TotalCount.ShouldBe(4);
		page.Items.Select(x => x.Id).ShouldBe(expected.Take(4).OrderByDescending(x => x.CreationTime).ThenBy(x => x.Id).Skip(1).Take(2).Select(x => x.Id));
		foreach (var item in page.Items) AssertSummary(item, expected.Single(x => x.Id == item.Id));
		input.SkipCount = 20;
		var emptyPage = await service.GetListAsync(input);
		emptyPage.TotalCount.ShouldBe(4);
		emptyPage.Items.ShouldBeEmpty();
		input.Search = "no-matching-application";
		(await service.GetListAsync(input)).TotalCount.ShouldBe(0);
		input.Search = "synthetic";
		input.Status = EmployeeApplicationStatus.Rejected;
		input.SkipCount = 0;
		(await service.GetListAsync(input)).Items.Single().Id.ShouldBe(expected[4].Id);
		foreach (var entity in expected) AssertSummary(EmployeeApplicationListProjection.Item.Compile()(entity), entity);
	}

	[Fact]
	public void PostgreSql_translation_uses_scalar_projection_without_owned_snapshots()
	{
		// SQL compilation only: no connection, database service, migrations or tenant data.
		using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
			.UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options);
		var sql = db.Set<EmployeeApplication>().IgnoreQueryFilters().Where(x => x.TenantId == Guid.Empty && !x.IsDeleted)
			.OrderByDescending(x => x.CreationTime).ThenBy(x => x.Id).Skip(20).Take(20)
			.Select(EmployeeApplicationListProjection.Item).ToQueryString();
		foreach (var column in new[] { "PersonalInfo", "FamilyInfo", "AdditionalInfo", "WorkExperiences", "EncryptedToken", "TokenHash" }) sql.ShouldNotContain(column);
		sql.ShouldContain("LIMIT");
		output.WriteLine(sql);
	}

	private new static EmployeeApplication Application(Guid tenantId, int index)
	{
		var now = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
		var entity = new EmployeeApplication(Guid.NewGuid(), tenantId, $"SYNTHETIC-{index:D4}", Guid.NewGuid().ToString(), "synthetic-encrypted-token",
			now.AddDays(30), now, " Synthetic ", index % 2 == 0 ? " " : "Middle", "Applicant", $"synthetic{index}@example.test", "1234567890", "Teacher", index % 2 == 0 ? "Legacy" : null);
		typeof(EmployeeApplication).GetProperty(nameof(EmployeeApplication.CreationTime))!.SetValue(entity, now.AddMinutes(index / 2));
		// Include legacy Unicode whitespace as well as present uploads without reading blobs.
		typeof(EmployeeApplication).GetProperty(nameof(EmployeeApplication.ResumeBlobName))!.SetValue(entity, index % 2 == 0 ? "synthetic-resume" : "\u2003");
		typeof(EmployeeApplication).GetProperty(nameof(EmployeeApplication.PhotoBlobName))!.SetValue(entity, index % 3 == 0 ? "synthetic-photo" : null);
		if (index % 4 == 0) entity.SetWorkAssignment(EmployeeWorkAssignmentModes.SchoolWide, null, null, [], []);
		if (index % 4 == 1) entity.SetWorkAssignment(EmployeeWorkAssignmentModes.DecideLater, null, null, [], []);
		if (index % 4 == 2) entity.SetWorkAssignment(EmployeeWorkAssignmentModes.SpecificOrganizations, Guid.NewGuid(), "2026-27", [Guid.NewGuid(), Guid.NewGuid()], ["Primary", "Secondary"]);
		return entity;
	}

	private static void AssertSummary(EmployeeApplicationListItemDto actual, EmployeeApplication entity)
	{
		actual.FullName.ShouldBe(entity.FullName);
		actual.WorkAssignmentDisplay.ShouldBe(entity.WorkAssignmentDisplay);
		actual.HasPhoto.ShouldBe(entity.HasPhoto);
		actual.HasResume.ShouldBe(entity.HasResume);
		actual.ApplicationNumber.ShouldBe(entity.ApplicationNumber);
		actual.Status.ShouldBe(entity.Status);
		actual.Email.ShouldBe(entity.Email);
		actual.MobileNo.ShouldBe(entity.MobileNo);
		actual.PositionAppliedFor.ShouldBe(entity.PositionAppliedFor);
		actual.DepartmentAppliedFor.ShouldBe(entity.DepartmentAppliedFor);
		actual.TokenExpiresAt.ShouldBe(entity.TokenExpiresAt);
		actual.LinkLastIssuedAt.ShouldBe(entity.LinkLastIssuedAt);
		actual.SubmittedAt.ShouldBe(entity.SubmittedAt);
		actual.FinalizedAt.ShouldBe(entity.FinalizedAt);
		actual.FinalizedEmployeeId.ShouldBe(entity.FinalizedEmployeeId);
		actual.CreationTime.ShouldBe(entity.CreationTime);
		if (entity.WorkAssignmentMode == null) actual.WorkAssignment.ShouldBeNull();
		else
		{
			actual.WorkAssignment!.Mode.ShouldBe(entity.WorkAssignmentMode);
			actual.WorkAssignment.AcademicYearId.ShouldBe(entity.WorkAssignmentAcademicYearId);
			actual.WorkAssignment.AcademicYearName.ShouldBe(entity.WorkAssignmentAcademicYearName);
			actual.WorkAssignment.OrganizationIds.ShouldBe(entity.WorkAssignmentOrganizationIds);
			actual.WorkAssignment.OrganizationLabels.ShouldBe(entity.WorkAssignmentOrganizationLabels);
		}
	}

	private static int ColumnCount(string sql) => Regex.Matches(sql[..sql.IndexOf("FROM", StringComparison.Ordinal)], "\"[a-z][a-z0-9]*\"\\.\"([^\"]+)\"")
		.Select(match => match.Groups[1].Value).Distinct().Count();
}
