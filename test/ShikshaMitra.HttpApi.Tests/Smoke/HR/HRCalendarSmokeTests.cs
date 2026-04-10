using System.Net;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Smoke.HR;

[TestModule(TestTraits.Modules.HR)]
[TestTier(TestTraits.Tiers.Smoke)]
public class HRCalendarSmokeTests : ShikshaMitraSmokeTestBase
{
	[SmokeFact]
	public async Task Smoke_Event_Types_List_Should_Return_200()
	{
		await AuthenticateAsync();

		var response = await Client.GetAsync("/api/calendar/event-types?SkipCount=0&MaxResultCount=5");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[SmokeFact]
	public async Task Smoke_Holiday_Types_List_Should_Return_200()
	{
		await AuthenticateAsync();

		var response = await Client.GetAsync("/api/calendar/holiday-types?SkipCount=0&MaxResultCount=5");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[SmokeFact]
	public async Task Smoke_Holidays_List_Should_Return_200()
	{
		await AuthenticateAsync();

		var response = await Client.GetAsync("/api/calendar/holidays?SkipCount=0&MaxResultCount=5");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}
}
