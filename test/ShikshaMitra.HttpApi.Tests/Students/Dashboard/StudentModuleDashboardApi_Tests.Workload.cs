using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
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
	public async Task Common_workspace_counts_only_owned_work_and_query_count_does_not_grow_with_population()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var id = await SeedOwnWorkAsync(10);
		using var client = Permissions(P.Dashboard.View, P.AdmissionInquiries.View, ShikshaMitraSubModulePermissions.Enrollment, P.StudentOnboarding.View, ShikshaMitraSubModulePermissions.StudentOnboarding);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, id.ToString());
		// A forged administrator claim must not change the database-resolved common mode.
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "admin");
		var context = await ReadContextAsync(client); context.Mode.ShouldBe("common");
		var counter = ((StudentModuleDashboardApiTestFactory)Factory).Counter;
		var url = $"{Root}/sections/admissions?academicYearId={Factory.SeededAcademicYearId}";
		counter.Begin(); var clock = Stopwatch.StartNew();
		var cold = await client.GetAsync(url); clock.Stop(); var coldQueries = counter.End();
		cold.StatusCode.ShouldBe(HttpStatusCode.OK, await cold.Content.ReadAsStringAsync());
		var small = await cold.Content.ReadFromJsonAsync<StudentModuleDashboardSectionDto>(JsonOptions);
		small!.Metrics.Single(metric => metric.Id == "inquiries").Value.ShouldBe(10);
		small.Metrics.Single(metric => metric.Id == "setup").Value.ShouldBe(1);
		await AddInquiriesAsync(id, 500);
		var samples = new List<double>(); var queryCounts = new List<int>();
		for (var index = 0; index < 23; index++)
		{
			counter.Begin(); var watch = Stopwatch.StartNew(); var response = await client.GetAsync(url); watch.Stop(); var commands = counter.End();
			response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
			var result = await response.Content.ReadFromJsonAsync<StudentModuleDashboardSectionDto>(JsonOptions);
			result!.Metrics.Single(metric => metric.Id == "inquiries").Value.ShouldBe(510);
			result.Metrics.Single(metric => metric.Id == "setup").Value.ShouldBe(1);
			(await response.Content.ReadAsByteArrayAsync()).Length.ShouldBeLessThan(16_384);
			commands.Count.ShouldBeLessThanOrEqualTo(35);
			commands.Count.ShouldBe(coldQueries.Count);
			if (index >= 3) { samples.Add(watch.Elapsed.TotalMilliseconds); queryCounts.Add(commands.Count); }
		}
		samples.Sort(); _output.WriteLine($"SQLite API fixture: 510 owned + 10 other-owner inquiries, 1 owned + 1 other-owner setup; cold={clock.Elapsed.TotalMilliseconds:F1}ms/{coldQueries.Count} queries; warmups=3 samples=20 p95={samples[18]:F1}ms queries={queryCounts.Max()}; payload<16KiB.");
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

	private async Task<Guid> SeedOwnWorkAsync(int inquiries)
	{
		var userId = await SeedUserAsync();
		await WithTenantAsync(async services =>
		{
			await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId), userId: userId), true);
			foreach (var owner in new[] { userId, Guid.Parse(TestAuthHandler.TestUserId) })
			{
				await SeedCaseAsync(services, owner);
			}
		});
		await AddInquiriesAsync(userId, inquiries, otherOwnerCount: 10);
		return userId;
	}

	private async Task AddInquiriesAsync(Guid userId, int ownedCount, int otherOwnerCount = 0) => await WithTenantAsync(async services =>
	{
		var cycleId = Guid.NewGuid();
		await services.GetRequiredService<IRepository<AdmissionCycle, Guid>>().InsertAsync(new AdmissionCycle(cycleId, Guid.Parse(TestAuthHandler.TestTenantId), "Dashboard fixture", Guid.NewGuid().ToString("N"), Factory.SeededAcademicYearId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(10)), true);
		var repository = services.GetRequiredService<IRepository<AdmissionInquiry, Guid>>();
		for (var index = 0; index < ownedCount + otherOwnerCount; index++)
		{
			var inquiry = new AdmissionInquiry(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId), Guid.NewGuid().ToString("N"), cycleId, null, "Fixture applicant", "9898989898", null, AdmissionInquirySource.WalkIn, null);
			inquiry.AssignCounsellor(index < ownedCount ? userId : Guid.Parse(TestAuthHandler.TestUserId)); await repository.InsertAsync(inquiry, false);
		}
	});

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
