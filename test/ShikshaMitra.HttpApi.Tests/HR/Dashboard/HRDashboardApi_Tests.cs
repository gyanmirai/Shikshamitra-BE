using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.ReportingManagerInfo;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Dashboard;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class HRDashboardApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/hr/dashboard";

	public HRDashboardApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_HR_Dashboard_Summary_With_Contract_Sections()
	{
		var response = await Client.GetAsync($"{BaseUrl}/summary?date=2026-05-23&month=2026-05&range=7");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var json = await response.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(json);

		document.RootElement.TryGetProperty("workforce", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("attendanceToday", out var attendanceToday).ShouldBeTrue();
		attendanceToday.TryGetProperty("missingAttendance", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("attendanceTrend", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("leave", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("payroll", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("celebrations", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("insights", out _).ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Show_Inactive_Reporting_Manager_Insight_For_Existing_Data()
	{
		var managerId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "DashboardInactiveMgr");
		var reportId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "DashboardInactiveReport");

		var assignResponse = await Client.PutAsJsonAsync(
			$"/api/employees/{reportId}/reporting-manager",
			new ReportingManagerRequest { ReportingManagerId = managerId.ToString() },
			JsonOptions);
		assignResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		using (var scope = Factory.Services.CreateScope())
		{
			var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
			var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
			var employeeManager = scope.ServiceProvider.GetRequiredService<EmployeeManager>();
			var employeeRepository = scope.ServiceProvider.GetRequiredService<IEmployeeRepository>();

			using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
			using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
			{
				var manager = await employeeManager.UpdateEmployeeStatusAsync(
					managerId,
					EmployeeStatusType.Inactive,
					"Legacy inconsistent state");
				await employeeRepository.UpdateAsync(manager);
				await uow.CompleteAsync();
			}
		}

		var response = await Client.GetAsync($"{BaseUrl}/summary?date=2026-05-23&month=2026-05&range=7");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var json = await response.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(json);
		var insights = document.RootElement.GetProperty("insights").EnumerateArray();
		var reportingManagerInsight = insights.FirstOrDefault(insight =>
			insight.GetProperty("source").GetString() == "employee.reporting-manager");

		reportingManagerInsight.ValueKind.ShouldNotBe(JsonValueKind.Undefined);
		reportingManagerInsight.GetProperty("severity").GetString().ShouldBe("critical");
		reportingManagerInsight.GetProperty("summary").GetString().ShouldContain("1 active employee");
		reportingManagerInsight.GetProperty("actionHref").GetString().ShouldBe("/hr/employee?reportingManagerStatus=nonActive");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Invalid_Dashboard_Date()
	{
		var response = await Client.GetAsync($"{BaseUrl}/summary?date=not-a-date&month=2026-05");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Invalid_Dashboard_Month()
	{
		var response = await Client.GetAsync($"{BaseUrl}/summary?date=2026-05-23&month=05-2026");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}
}
