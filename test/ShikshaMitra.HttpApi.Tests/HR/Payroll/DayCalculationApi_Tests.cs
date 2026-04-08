using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class DayCalculationApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/payroll/day-calculation";

	public DayCalculationApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Save_Calendar_Day_Calculation()
	{
		var input = new DayCalculationRequestDto
		{
			DayCalculationType = "CALENDAR",
			ManualDays = null,
		};

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<DayCalculationResponseDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Success.ShouldBeTrue();
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Save_Manual_Day_Calculation()
	{
		var input = new DayCalculationRequestDto
		{
			DayCalculationType = "MANUAL",
			ManualDays = 26,
		};

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<DayCalculationResponseDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Success.ShouldBeTrue();
	}

	#endregion
}
