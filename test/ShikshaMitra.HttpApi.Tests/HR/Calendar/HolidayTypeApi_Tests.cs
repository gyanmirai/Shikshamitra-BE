using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace ShikshaMitra.HR.Calendar;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class HolidayTypeApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/calendar/holiday-types";

	public HolidayTypeApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 0 - Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Holiday_Type()
	{
		var input = new CreateUpdateHolidayTypeDto
		{
			Name = TestDataHelper.UniqueName("NationalHol"),
			Color = "#00BFFF",
			Description = "National holiday type",
		};

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<HolidayTypeDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Id.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Holiday_Type_By_Id()
	{
		var created = await CalendarTestHelper.CreateHolidayTypeAsync(Client, "GetHT");

		var response = await Client.GetAsync($"{BaseUrl}/{created.Id}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<HolidayTypeDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Id.ShouldBe(created.Id);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Holiday_Type_List()
	{
		await CalendarTestHelper.CreateHolidayTypeAsync(Client, "ListHT");

		var response = await Client.GetAsync($"{BaseUrl}?SkipCount=0&MaxResultCount=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<PagedResultDto<HolidayTypeDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldNotBeNull();
		body.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Update_Holiday_Type()
	{
		var created = await CalendarTestHelper.CreateHolidayTypeAsync(Client, "UpdateHT");

		var updateDto = new CreateUpdateHolidayTypeDto
		{
			Name = TestDataHelper.UniqueName("UpdatedHolType"),
			Color = "#DC3545",
			Description = "Updated description",
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", updateDto, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	#endregion

	#region Tier 1 - Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Delete_Holiday_Type()
	{
		var created = await CalendarTestHelper.CreateHolidayTypeAsync(Client, "DeleteHT");

		var response = await Client.DeleteAsync($"{BaseUrl}/{created.Id}");

		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_404_For_Nonexistent()
	{
		var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	#endregion
}
