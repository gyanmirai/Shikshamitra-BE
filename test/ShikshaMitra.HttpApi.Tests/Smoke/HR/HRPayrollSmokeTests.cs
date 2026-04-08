using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Smoke.HR;

/// <summary>
/// Hosted smoke tests that run against a real server with real OpenIddict tokens.
/// Requires the target host to be running. These validate the full auth + network pipeline.
///
/// Run with: dotnet test --filter "Tier=Smoke"
/// Set APITEST_BASE_URL environment variable to target a different host.
/// </summary>
[TestModule(TestTraits.Modules.HR)]
[TestTier(TestTraits.Tiers.Smoke)]
public class HRPayrollSmokeTests : ShikshaMitraSmokeTestBase
{
	private const string EarningComponentsUrl = "/api/payroll/earning-components";
	private const string DeductionComponentsUrl = "/api/payroll/deduction-components";

	[Fact]
	public async Task Smoke_Earning_Components_List_Should_Return_200()
	{
		await AuthenticateAsync();

		var response = await Client.GetAsync($"{EarningComponentsUrl}?page=1&pageSize=5");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<EarningComponentPaginatedResponseDto>();
		body.ShouldNotBeNull();
	}

	[Fact]
	public async Task Smoke_Deduction_Components_List_Should_Return_200()
	{
		await AuthenticateAsync();

		var response = await Client.GetAsync($"{DeductionComponentsUrl}?page=1&pageSize=5");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<DeductionComponentPaginatedResponseDto>();
		body.ShouldNotBeNull();
	}

	[Fact]
	public async Task Smoke_Unauthenticated_Request_Should_Return_401()
	{
		var plainClient = new System.Net.Http.HttpClient(
			new System.Net.Http.HttpClientHandler
			{
				ServerCertificateCustomValidationCallback =
					System.Net.Http.HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
			})
		{
			BaseAddress = Client.BaseAddress
		};

		var response = await plainClient.GetAsync($"{EarningComponentsUrl}?page=1&pageSize=5");

		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		plainClient.Dispose();
	}
}
