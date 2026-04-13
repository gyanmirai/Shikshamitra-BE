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
	public async Task Should_Save_WorkingDays_DayCalculation()
	{
		var input = new DayCalculationRequestDto
		{
			DayCalculationType = "working-days",
		};

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<DayCalculationResponseDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Success.ShouldBeTrue();
		body.Message.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Save_TotalMonthDays_DayCalculation()
	{
		var input = new DayCalculationRequestDto
		{
			DayCalculationType = "total-month-days",
		};

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<DayCalculationResponseDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Success.ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Save_ManualDays_DayCalculation()
	{
		var input = new DayCalculationRequestDto
		{
			DayCalculationType = "manual-days",
			ManualDays = 26,
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
	public async Task Should_Reject_Invalid_DayCalculationType()
	{
		var input = new DayCalculationRequestDto
		{
			DayCalculationType = "CALENDAR",
		};

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Empty_DayCalculationType()
	{
		var input = new DayCalculationRequestDto
		{
			DayCalculationType = "",
		};

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Upsert_Overwrite_Existing_Setting()
	{
		var first = new DayCalculationRequestDto { DayCalculationType = "working-days" };
		var firstResponse = await Client.PostAsJsonAsync(BaseUrl, first, JsonOptions);
		firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var second = new DayCalculationRequestDto
		{
			DayCalculationType = "manual-days",
			ManualDays = 22,
		};
		var secondResponse = await Client.PostAsJsonAsync(BaseUrl, second, JsonOptions);

		secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await secondResponse.Content.ReadFromJsonAsync<DayCalculationResponseDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Success.ShouldBeTrue();
	}

	#endregion

	#region Tier 2 — Extended

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Accept_Case_Insensitive_DayCalculationType()
	{
		var input = new DayCalculationRequestDto
		{
			DayCalculationType = "Working-Days",
		};

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<DayCalculationResponseDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Success.ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Save_ManualDays_At_Boundary_Values()
	{
		var min = new DayCalculationRequestDto
		{
			DayCalculationType = "manual-days",
			ManualDays = 1,
		};
		var minResponse = await Client.PostAsJsonAsync(BaseUrl, min, JsonOptions);
		minResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var max = new DayCalculationRequestDto
		{
			DayCalculationType = "manual-days",
			ManualDays = 31,
		};
		var maxResponse = await Client.PostAsJsonAsync(BaseUrl, max, JsonOptions);
		maxResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Cycle_Through_All_DayCalculation_Types()
	{
		var types = new (string type, int? days)[]
		{
			("working-days", null),
			("total-month-days", null),
			("manual-days", 20),
			("working-days", null),
		};

		foreach (var (type, days) in types)
		{
			var input = new DayCalculationRequestDto
			{
				DayCalculationType = type,
				ManualDays = days,
			};

			var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
			response.StatusCode.ShouldBe(HttpStatusCode.OK);
		}
	}

	#endregion
}
