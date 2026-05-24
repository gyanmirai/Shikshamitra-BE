using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class RunPayrollApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/payroll/run";
	private const string ReviewUrl = "/api/payroll/run/reviews";
	private const string PayrollMonth = "APRIL-2026";

	public RunPayrollApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Payroll_Preview_Without_500()
	{
		var response = await Client.GetAsync($"{BaseUrl}?month=2026-05&page=0&pageSize=100");
		var body = await response.Content.ReadAsStringAsync();

		body.ShouldNotContain("HR:Payroll:030011");
		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.OK,
			HttpStatusCode.BadRequest,
			HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Payroll_Readiness_Without_Generating_Preview()
	{
		var response = await Client.GetAsync($"{BaseUrl}/readiness?month=2026-05");

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.OK,
			HttpStatusCode.BadRequest,
			HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Lock_Payroll_Without_500()
	{
		var response = await Client.PostAsJsonAsync(
			BaseUrl,
			new RunPayrollRequest { Month = PayrollMonth },
			JsonOptions);

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.OK,
			HttpStatusCode.BadRequest,
			HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Mark_Review_Without_500()
	{
		var response = await Client.PostAsJsonAsync(
			ReviewUrl,
			new PayrollRunReviewRequest
			{
				Month = PayrollMonth,
				EmployeeIds = new List<System.Guid> { System.Guid.NewGuid() }
			},
			JsonOptions);

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.OK,
			HttpStatusCode.BadRequest,
			HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Unmark_Review_Without_500()
	{
		var response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, ReviewUrl)
		{
			Content = JsonContent.Create(
				new PayrollRunReviewRequest
				{
					Month = PayrollMonth,
					EmployeeIds = new List<System.Guid> { System.Guid.NewGuid() }
				},
				options: JsonOptions)
		});

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.OK,
			HttpStatusCode.BadRequest,
			HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Error_For_Invalid_Month_Format()
	{
		var response = await Client.GetAsync($"{BaseUrl}?month=invalid-format&page=0&pageSize=50");
		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.BadRequest,
			HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task HrSwagger_Should_Contain_RunPayroll_And_AutomatedRunLock_Endpoints()
	{
		var response = await Client.GetAsync("/swagger/v1-hr/swagger.json");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var json = await response.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(json);
		document.RootElement.GetProperty("paths").TryGetProperty("/api/payroll/run", out _).ShouldBeTrue();
		document.RootElement.GetProperty("paths").TryGetProperty("/api/payroll/run/readiness", out _).ShouldBeTrue();
		document.RootElement.GetProperty("paths").TryGetProperty("/api/payroll/run/reviews", out _).ShouldBeTrue();
		document.RootElement.GetProperty("paths").TryGetProperty("/api/hr/attendance/automated-runs/{batchId}/lock", out _).ShouldBeTrue();
	}
}
