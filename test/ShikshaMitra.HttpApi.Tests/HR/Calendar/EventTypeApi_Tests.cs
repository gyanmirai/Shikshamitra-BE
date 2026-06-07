using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Calendar.EventTypes;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace ShikshaMitra.HR.Calendar;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class EventTypeApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/calendar/event-types";

	public EventTypeApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 0 - Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Event_Type()
	{
		var input = new CreateUpdateEventTypeDto
		{
			Name = TestDataHelper.UniqueName("AnnualDay"),
			Color = "#FF5733",
			Description = "Annual day celebration",
		};

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<EventTypeDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Id.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Event_Type_By_Id()
	{
		var created = await CalendarTestHelper.CreateEventTypeAsync(Client, "GetById");

		var response = await Client.GetAsync($"{BaseUrl}/{created.Id}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<EventTypeDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Id.ShouldBe(created.Id);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Event_Type_List()
	{
		await CalendarTestHelper.CreateEventTypeAsync(Client, "List");

		var response = await Client.GetAsync($"{BaseUrl}?SkipCount=0&MaxResultCount=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<PagedResultDto<EventTypeDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldNotBeNull();
		body.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Update_Event_Type()
	{
		var created = await CalendarTestHelper.CreateEventTypeAsync(Client, "Update");

		var updateDto = new CreateUpdateEventTypeDto
		{
			Name = TestDataHelper.UniqueName("UpdatedEvt"),
			Color = "#28A745",
			Description = "Updated description",
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", updateDto, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	#endregion

	#region Tier 1 - Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Delete_Event_Type()
	{
		var created = await CalendarTestHelper.CreateEventTypeAsync(Client, "Delete");

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
