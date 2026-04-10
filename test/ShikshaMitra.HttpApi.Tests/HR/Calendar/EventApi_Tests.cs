using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.HR.Calendar.Events;
using ShikshaMitra.HR.Calendar.Events;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace ShikshaMitra.HR.Calendar;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class EventApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/calendar/events";

	public EventApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Event()
	{
		var eventType = await CalendarTestHelper.CreateEventTypeAsync(Client, "EvtCreate");
		var academicYearId = Factory.SeededAcademicYearId;

		var input = new CreateUpdateEventDto
		{
			AcademicYearId = academicYearId,
			Title = TestDataHelper.UniqueName("AnnualDay"),
			EventTypeId = eventType.Id,
			Mode = EventMode.Offline,
			Venue = "School Auditorium",
			StartTime = "09:00",
			EndTime = "17:00",
			IsRecurring = false,
			AudienceIds = new List<Guid>(),
		};

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}?academicYearId={academicYearId}", input, JsonOptions);

		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<EventDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Id.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Event_By_Id()
	{
		var created = await CreateEventAsync("GetEvt");

		var response = await Client.GetAsync($"{BaseUrl}/{created.Id}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<EventDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Id.ShouldBe(created.Id);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Event_List()
	{
		await CreateEventAsync("ListEvt");

		var response = await Client.GetAsync($"{BaseUrl}?SkipCount=0&MaxResultCount=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<PagedResultDto<EventDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldNotBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Update_Event()
	{
		var created = await CreateEventAsync("UpdEvt");

		var updateDto = new CreateUpdateEventDto
		{
			AcademicYearId = created.AcademicYearId,
			Title = TestDataHelper.UniqueName("UpdatedEvt"),
			EventTypeId = created.EventTypeId,
			Mode = EventMode.Online,
			StartTime = "10:00",
			EndTime = "12:00",
			IsRecurring = false,
			AudienceIds = new List<Guid>(),
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", updateDto, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Delete_Event()
	{
		var created = await CreateEventAsync("DelEvt");

		var response = await Client.DeleteAsync($"{BaseUrl}/{created.Id}");

		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Events_By_Type()
	{
		var created = await CreateEventAsync("ByTypeEvt");

		var response = await Client.GetAsync($"{BaseUrl}/by-type/{created.EventTypeId}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Events_By_Status()
	{
		await CreateEventAsync("ByStatusEvt");

		var response = await Client.GetAsync($"{BaseUrl}/by-status/0");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_404_For_Nonexistent()
	{
		var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	#endregion

	#region Helpers

	private async Task<EventDto> CreateEventAsync(string suffix)
	{
		var eventType = await CalendarTestHelper.CreateEventTypeAsync(Client, suffix);
		var academicYearId = Factory.SeededAcademicYearId;

		var input = new CreateUpdateEventDto
		{
			AcademicYearId = academicYearId,
			Title = TestDataHelper.UniqueName(suffix),
			EventTypeId = eventType.Id,
			Mode = EventMode.Offline,
			Venue = "Test Venue",
			StartTime = "09:00",
			EndTime = "17:00",
			IsRecurring = false,
			AudienceIds = new List<Guid>(),
		};

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}?academicYearId={academicYearId}", input, JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<EventDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create event");
	}

	#endregion
}
