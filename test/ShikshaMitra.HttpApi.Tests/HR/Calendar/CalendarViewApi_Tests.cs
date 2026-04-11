using System;
using System.Net;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Calendar;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class CalendarViewApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/calendar";

	public CalendarViewApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Calendar_View()
	{
		var academicYearId = Guid.NewGuid();

		var response = await Client.GetAsync(
			$"{BaseUrl}/view?academicYearId={academicYearId}&page=1&pageSize=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Item_Details_Returns_404_For_Nonexistent()
	{
		var response = await Client.GetAsync($"{BaseUrl}/items/{Guid.NewGuid()}");

		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	#endregion
}
