using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;
using P = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students;

namespace ShikshaMitra.Students.Dashboard;

public partial class StudentModuleDashboardApi_Tests
{
	[Fact]
	public async Task Common_workspace_counts_scoped_work_independently_of_legacy_assignees_without_query_growth()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var (id, yearId) = await SeedScopedWorkAsync(10);
		using var client = Permissions(P.Dashboard.View, P.AdmissionInquiries.View, ShikshaMitraSubModulePermissions.Enrollment, P.StudentOnboarding.View, ShikshaMitraSubModulePermissions.StudentOnboarding);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, id.ToString());
		// A forged administrator claim must not change the database-resolved common mode.
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "admin");
		var context = await ReadContextAsync(client); context.Mode.ShouldBe("common");
		var counter = ((StudentModuleDashboardApiTestFactory)Factory).Counter;
		var url = $"{Root}/sections/admissions?academicYearId={yearId}";
		counter.Begin(); var clock = Stopwatch.StartNew();
		var cold = await client.GetAsync(url); clock.Stop(); var coldQueries = counter.End();
		cold.StatusCode.ShouldBe(HttpStatusCode.OK, await cold.Content.ReadAsStringAsync());
		var small = await cold.Content.ReadFromJsonAsync<StudentModuleDashboardSectionDto>(JsonOptions);
		// Assignment was retired by f5c49ed5: both legacy assignees are visible within this granted year.
		small!.Metrics.Single(metric => metric.Id == "inquiries").Value.ShouldBe(20);
		small.Metrics.Single(metric => metric.Id == "setup").Value.ShouldBe(2);
		small.Metrics.Single(metric => metric.Id == "inquiries").Label.ShouldBe("Open inquiries");
		small.Metrics.Single(metric => metric.Id == "inquiries").Href.ShouldBe("/student/admissions/inquiries");
		small.Metrics.Single(metric => metric.Id == "setup").Href.ShouldBe("/student/onboarding/setup");
		await AddInquiriesAsync(id, 500, academicYearId: yearId);
		var samples = new List<double>(); var queryCounts = new List<int>();
		for (var index = 0; index < 23; index++)
		{
			counter.Begin(); var watch = Stopwatch.StartNew(); var response = await client.GetAsync(url); watch.Stop(); var commands = counter.End();
			response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
			var result = await response.Content.ReadFromJsonAsync<StudentModuleDashboardSectionDto>(JsonOptions);
			result!.Metrics.Single(metric => metric.Id == "inquiries").Value.ShouldBe(520);
			result.Metrics.Single(metric => metric.Id == "setup").Value.ShouldBe(2);
			(await response.Content.ReadAsByteArrayAsync()).Length.ShouldBeLessThan(16_384);
			commands.Count.ShouldBeLessThanOrEqualTo(35);
			commands.Count.ShouldBe(coldQueries.Count);
			if (index >= 3) { samples.Add(watch.Elapsed.TotalMilliseconds); queryCounts.Add(commands.Count); }
		}
		samples.Sort(); _output.WriteLine($"SQLite API fixture: isolated year, 510 current-user legacy-assigned + 10 other-assignee inquiries, 2 scoped setup cases; cold={clock.Elapsed.TotalMilliseconds:F1}ms/{coldQueries.Count} queries; warmups=3 samples=20 p95={samples[18]:F1}ms queries={queryCounts.Max()}; payload<16KiB.");
	}

	[Fact]
	public async Task Common_workspace_includes_ownerless_and_other_assignee_work_only_within_academic_scope()
	{
		var id = await SeedUserAsync();
		Guid allowedYear = default, excludedYear = default;
		await WithTenantAsync(async services =>
		{
			var allowed = await CreateDashboardAcademicPathAsync(services);
			var excluded = await CreateDashboardAcademicPathAsync(services);
			allowedYear = allowed.YearId; excludedYear = excluded.YearId;
			await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(
				new UserScopeAssignment(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId), userId: id, academicYearId: allowedYear), true);
			foreach (var legacyOwner in new Guid?[] { id, Guid.Parse(TestAuthHandler.TestUserId), null })
			{
				var item = await SeedCaseAsync(services, legacyOwner, academicPath: allowed);
				await SeedDashboardInquiryForCaseAsync(services, item, legacyOwner, AdmissionInquiryStatus.New);
			}
			var cancelled = await SeedCaseAsync(services, id, academicPath: allowed);
			cancelled.Cancel("Closed fixture case", DateTime.UtcNow);
			await services.GetRequiredService<IRepository<StudentOnboardingCase, Guid>>().UpdateAsync(cancelled, true);
			await SeedDashboardInquiryForCaseAsync(services, cancelled, id, AdmissionInquiryStatus.NotInterested);
			var outside = await SeedCaseAsync(services, id, academicPath: excluded);
			await SeedDashboardInquiryForCaseAsync(services, outside, id, AdmissionInquiryStatus.New);
		});
		using var client = Permissions(P.Dashboard.View, P.AdmissionInquiries.View, ShikshaMitraSubModulePermissions.Enrollment,
			P.StudentOnboarding.View, ShikshaMitraSubModulePermissions.StudentOnboarding);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, id.ToString());
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "admin");
		(await ReadContextAsync(client)).Mode.ShouldBe("common");
		var response = await client.GetAsync($"{Root}/sections/admissions?academicYearId={allowedYear}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var result = (await response.Content.ReadFromJsonAsync<StudentModuleDashboardSectionDto>(JsonOptions))!;
		result.Metrics.Single(metric => metric.Id == "inquiries").Value.ShouldBe(3);
		result.Metrics.Single(metric => metric.Id == "setup").Value.ShouldBe(3);
		result.Metrics.Single(metric => metric.Id == "inquiries").Label.ShouldBe("Open inquiries");
		result.Metrics.Single(metric => metric.Id == "inquiries").Href.ShouldBe("/student/admissions/inquiries");
		result.Description.ShouldContain("academic scope");
		var inquiries = await client.GetAsync($"/api/student/enrollment/admissions/inquiries?academicYearId={allowedYear}&status=0&maxResultCount=100");
		inquiries.StatusCode.ShouldBe(HttpStatusCode.OK, await inquiries.Content.ReadAsStringAsync());
		using var json = JsonDocument.Parse(await inquiries.Content.ReadAsStringAsync());
		json.RootElement.GetProperty("data").GetProperty("totalCount").GetInt32().ShouldBe(3,
			"The Common summary and owning scoped inquiry list must agree regardless of legacy assignee.");
		(await client.GetAsync($"{Root}/sections/admissions?academicYearId={excludedYear}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden,
			"A legacy self-assignment and forged Admin claim cannot grant a different academic year.");
	}

	[Fact]
	public async Task A_user_without_academic_scope_cannot_read_any_section_even_with_admin_claims()
	{
		var id = await SeedUserAsync();
		using var client = Permissions(P.Dashboard.View, P.Records_StudentList.View, ShikshaMitraSubModulePermissions.Records);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, id.ToString());
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "admin");
		(await client.GetAsync($"{Root}/sections/records?academicYearId={Factory.SeededAcademicYearId}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	private async Task<(Guid UserId, Guid AcademicYearId)> SeedScopedWorkAsync(int inquiries)
	{
		var userId = await SeedUserAsync();
		Guid yearId = default;
		await WithTenantAsync(async services =>
		{
			var path = await CreateDashboardAcademicPathAsync(services); yearId = path.YearId;
			await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId), userId: userId), true);
			foreach (var owner in new[] { userId, Guid.Parse(TestAuthHandler.TestUserId) })
			{
				await SeedCaseAsync(services, owner, academicPath: path);
			}
		});
		await AddInquiriesAsync(userId, inquiries, otherOwnerCount: 10, academicYearId: yearId);
		return (userId, yearId);
	}

	private async Task AddInquiriesAsync(Guid userId, int ownedCount, int otherOwnerCount = 0, Guid? academicYearId = null) => await WithTenantAsync(async services =>
	{
		var cycleId = Guid.NewGuid();
		await services.GetRequiredService<IRepository<AdmissionCycle, Guid>>().InsertAsync(new AdmissionCycle(cycleId, Guid.Parse(TestAuthHandler.TestTenantId), "Dashboard fixture", Guid.NewGuid().ToString("N"), academicYearId ?? Factory.SeededAcademicYearId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(10)), true);
		var repository = services.GetRequiredService<IRepository<AdmissionInquiry, Guid>>();
		for (var index = 0; index < ownedCount + otherOwnerCount; index++)
		{
			var inquiry = new AdmissionInquiry(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId), Guid.NewGuid().ToString("N"), cycleId, null, "Fixture applicant", "9898989898", null, AdmissionInquirySource.WalkIn, null);
			inquiry.AssignCounsellor(index < ownedCount ? userId : Guid.Parse(TestAuthHandler.TestUserId)); await repository.InsertAsync(inquiry, false);
		}
	});

	private static async Task SeedDashboardInquiryForCaseAsync(IServiceProvider services, StudentOnboardingCase item, Guid? legacyOwner, AdmissionInquiryStatus status)
	{
		var offering = await services.GetRequiredService<IRepository<AdmissionOffering, Guid>>().GetAsync(item.OfferingId);
		var inquiry = new AdmissionInquiry(Guid.NewGuid(), item.TenantId, Guid.NewGuid().ToString("N"), offering.AdmissionCycleId,
			offering.Id, "Scoped fixture applicant", "9898989898", null, AdmissionInquirySource.WalkIn, null);
		if (legacyOwner.HasValue) inquiry.AssignCounsellor(legacyOwner.Value);
		if (status != AdmissionInquiryStatus.New) inquiry.SetStatus(status);
		await services.GetRequiredService<IRepository<AdmissionInquiry, Guid>>().InsertAsync(inquiry, true);
	}

	private async Task<Guid> SeedUserAsync()
	{
		var id = Guid.NewGuid();
		await WithTenantAsync(services => services.GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(new IdentityUser(id, "dash" + id.ToString("N"), "dash" + id.ToString("N") + "@example.test", Guid.Parse(TestAuthHandler.TestTenantId)), true));
		return id;
	}

	private async Task WithTenantAsync(Func<IServiceProvider, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		await action(scope.ServiceProvider); await uow.CompleteAsync();
	}
}
