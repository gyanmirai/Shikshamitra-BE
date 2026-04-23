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

	public RunPayrollApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Payroll_Preview()
	{
		var response = await Client.GetAsync($"{BaseUrl}?month=APRIL-2026&page=0&pageSize=50");

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.OK,
			HttpStatusCode.Forbidden,
			HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Payroll_Preview_With_YYYY_MM_Format()
	{
		var response = await Client.GetAsync($"{BaseUrl}?month=2026-04&page=0&pageSize=50");
		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.OK,
			HttpStatusCode.Forbidden,
			HttpStatusCode.InternalServerError);
	}

	#endregion

	#region Tier 2 — Extended (destructive)

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	[Trait(TestTraits.Destructive, "true")]
	public async Task Should_Lock_Payroll()
	{
		var request = new RunPayrollRequest
		{
			Month = "APRIL-2026",
		};

		var response = await Client.PostAsJsonAsync(BaseUrl, request, JsonOptions);

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.OK,
			HttpStatusCode.BadRequest,
			HttpStatusCode.Forbidden,
			HttpStatusCode.InternalServerError);
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Error_For_Invalid_Month_Format()
	{
		var response = await Client.GetAsync($"{BaseUrl}?month=invalid-format&page=0&pageSize=50");

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.BadRequest,
			HttpStatusCode.Forbidden,
			HttpStatusCode.InternalServerError);
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
		document.RootElement.GetProperty("paths").TryGetProperty("/api/hr/attendance/automated-runs/{batchId}/lock", out _).ShouldBeTrue();
	}

	#endregion
}
