using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Dashboard;

public partial class HRDashboardApi_Tests
{
	private const string AttendanceView = "HR.AttendanceAndLeave.Dashboard.View";
	private const string TrackingView = "HR.AttendanceAndLeave.AttendanceTracking.View";

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Attendance_Dashboard_Should_Use_Its_Own_Grant_And_Return_Only_Counts_Without_Tracking()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, AttendanceView);

		using var response = await client.GetAsync($"{BaseUrl}/attendance?date=2026-05-23");
		var raw = await response.Content.ReadAsStringAsync();
		response.StatusCode.ShouldBe(HttpStatusCode.OK, raw);
		response.Headers.CacheControl!.NoStore.ShouldBeTrue();
		using var json = JsonDocument.Parse(raw);
		json.RootElement.TryGetProperty("review", out _).ShouldBeFalse();
		raw.ShouldNotContain("employeeId");
		raw.ShouldNotContain("timeIn");
		var result = JsonSerializer.Deserialize<HRAttendanceDashboardDto>(raw, JsonOptions)!;
		result.Trend.Select(point => point.Date).ShouldBe(Enumerable.Range(17, 7).Select(day => $"2026-05-{day}"));
		var summary = result.Summary;
		(summary.Present + summary.Absent + summary.OnLeave + summary.HalfDay + summary.NotMarked +
			summary.NotScheduled + summary.NeedsSetup).ShouldBe(summary.StaffCount);
		result.Trend[^1].StaffCount.ShouldBe(summary.StaffCount);

		client.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, $"HR.Dashboard.View,{TrackingView}");
		(await client.GetAsync($"{BaseUrl}/attendance")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var anonymous = CreateAnonymousClient();
		(await anonymous.GetAsync($"{BaseUrl}/attendance")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Attendance_Dashboard_Should_Apply_Scope_To_Named_Review_And_Revalidate_It_On_The_Next_Read()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		using var services = Factory.Services.CreateScope();
		var tenant = services.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var units = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var scopeAssignments = services.ServiceProvider.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		DateOnly date;
		using (tenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var unit = units.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var year = await services.ServiceProvider.GetRequiredService<IRepository<AcademicYear, Guid>>()
				.GetAsync(EmployeeTestHelper.DefaultAcademicYearId);
			date = DateOnly.FromDateTime(year.StartDate).AddDays(7);
			await unit.CompleteAsync();
		}

		var allowed = await EmployeeTestHelper.CreateEmployeeAsync(Client, $"AAReview{Guid.NewGuid():N}",
			dateOfJoining: date.ToDateTime(TimeOnly.MinValue));
		await AssignStaffOrganizationAsync(allowed.Id, EmployeeTestHelper.DefaultMediumStageId);
		var outside = await EmployeeTestHelper.CreateEmployeeAsync(Client, $"OutsideReview{Guid.NewGuid():N}",
			dateOfJoining: date.ToDateTime(TimeOnly.MinValue));
		using (tenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var unit = units.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await services.ServiceProvider.GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>()
				.DeleteAsync(assignment => assignment.EmployeeId == outside.Id);
			var records = services.ServiceProvider.GetRequiredService<IRepository<EmployeeAttendance, Guid>>();
			foreach (var employeeId in new[] { allowed.Id, outside.Id })
			{
				await records.InsertAsync(new EmployeeAttendance
				{
					TenantId = tenant.Id, EmployeeId = employeeId, AttendanceDate = date,
					StatusType = EmployeeAttendanceStatusType.Present, Source = AttendanceRecordSource.Integration,
					TimeIn = new DateTimeOffset(date.ToDateTime(new TimeOnly(9)), TimeSpan.FromHours(5.5))
				});
			}
			await unit.CompleteAsync();
		}

		var userId = Guid.NewGuid();
		await EnsureUserHasStageScopeAsync(userId, EmployeeTestHelper.DefaultMediumStageId);
		using var client = CreateClientAs(userId);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, $"{AttendanceView},{TrackingView}");
		var url = $"{BaseUrl}/attendance?date={date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
		var response = await client.GetAsync(url);
		var raw = await response.Content.ReadAsStringAsync();
		response.StatusCode.ShouldBe(HttpStatusCode.OK, raw);
		var result = JsonSerializer.Deserialize<HRAttendanceDashboardDto>(raw, JsonOptions)!;
		result.ScopeLabel.ShouldBe("Staff in your assigned scope");
		result.Review!.Count.ShouldBeLessThanOrEqualTo(5);
		result.Review.ShouldContain(row => row.EmployeeId == allowed.Id &&
			row.Flags.Any(flag => flag.Code == HRDashboardReviewFlagCode.IncompletePunch), raw);
		raw.ShouldNotContain(outside.Id.ToString());
		raw.ShouldNotContain("timeIn");

		client.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, AttendanceView);
		var reduced = await client.GetFromJsonAsync<HRAttendanceDashboardDto>(url, JsonOptions);
		reduced!.Review.ShouldBeNull();
		reduced.Summary.NeedsReview.ShouldBe(result.Summary.NeedsReview);
		using (tenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var unit = units.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await scopeAssignments.DeleteAsync(assignment => assignment.UserId == userId);
			await unit.CompleteAsync();
		}
		(await client.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Theory]
	[InlineData("date=0001-01-01")]
	[InlineData("date=2026-02-30")]
	[InlineData("date=2099-01-01")]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Attendance_Dashboard_Should_Reject_Invalid_Date(string query)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		(await Client.GetAsync($"{BaseUrl}/attendance?{query}")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Legacy_Attendance_Preview_Should_Redact_Stored_Names_On_Read_And_Refresh()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await ClearDashboardPreviewsAsync();
		using var services = Factory.Services.CreateScope();
		var tenant = services.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var units = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		using (tenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var unit = units.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var preview = new HrDashboardPreview(Guid.NewGuid(), tenant.Id, HrDashboardKeys.Attendance, HrDashboardScopeKeys.HrWide);
			preview.MarkReady("""{"totalStaff":4,"topDefaulters":[{"employeeName":"Private Test Employee"}]}""", 1, DateTime.UtcNow);
			await services.ServiceProvider.GetRequiredService<IRepository<HrDashboardPreview, Guid>>()
				.InsertAsync(preview);
			await unit.CompleteAsync();
		}

		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, $"HR.Dashboard.View,{AttendanceView}");
		var url = $"{BaseUrl}/previews/Attendance";
		using var read = await client.GetAsync(url);
		using var refresh = await client.PostAsync($"{url}/refresh", null);
		foreach (var response in new[] { read, refresh })
		{
			response.StatusCode.ShouldBe(HttpStatusCode.OK);
			var preview = (await response.Content.ReadFromJsonAsync<HRDashboardPreviewDto>(JsonOptions))!;
			using var payload = JsonDocument.Parse(preview.PayloadJson);
			payload.RootElement.GetProperty("totalStaff").GetInt32().ShouldBe(4);
			payload.RootElement.GetProperty("topDefaulters").GetArrayLength().ShouldBe(0);
		}
	}
}
