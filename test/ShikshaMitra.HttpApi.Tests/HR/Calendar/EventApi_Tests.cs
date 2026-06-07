using System;
using System.Collections.Generic;
using System.Linq;
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
			StartDate = DateTimeOffset.UtcNow,
			EndDate = DateTimeOffset.UtcNow,
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
		body.StartDate.ShouldNotBeNull();
		body.EndDate.ShouldNotBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Meeting_Event_With_Meeting_Date_Only()
	{
		var eventType = await CalendarTestHelper.GetOrCreateMeetingEventTypeAsync(Client);
		var academicYearId = Factory.SeededAcademicYearId;
		var meetingDate = DateTimeOffset.UtcNow.AddDays(3);

		var input = new CreateUpdateEventDto
		{
			AcademicYearId = academicYearId,
			Title = TestDataHelper.UniqueName("MeetingEvt"),
			EventTypeId = eventType.Id,
			Mode = EventMode.Online,
			MeetingDate = meetingDate,
			StartTime = "09:00",
			EndTime = "10:00",
			IsRecurring = false,
			AudienceIds = new List<Guid>(),
		};

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}?academicYearId={academicYearId}", input, JsonOptions);

		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<EventDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.MeetingDate.ShouldNotBeNull();
		body.MeetingDate!.Value.Date.ShouldBe(meetingDate.Date);
		body.StartDate.ShouldNotBeNull();
		body.EndDate.ShouldNotBeNull();
		body.StartDate!.Value.Date.ShouldBe(meetingDate.Date);
		body.EndDate!.Value.Date.ShouldBe(meetingDate.Date);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Non_Meeting_Event_Without_Date_Range()
	{
		var eventType = await CalendarTestHelper.CreateEventTypeAsync(Client, "NoRangeEvt");
		var academicYearId = Factory.SeededAcademicYearId;

		var input = new CreateUpdateEventDto
		{
			AcademicYearId = academicYearId,
			Title = TestDataHelper.UniqueName("NoRangeEvt"),
			EventTypeId = eventType.Id,
			Mode = EventMode.Online,
			StartTime = "09:00",
			EndTime = "10:00",
			IsRecurring = false,
			AudienceIds = new List<Guid>(),
		};

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}?academicYearId={academicYearId}", input, JsonOptions);

		response.IsSuccessStatusCode.ShouldBeFalse();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Non_Meeting_Event_With_EndDate_Before_StartDate()
	{
		var eventType = await CalendarTestHelper.CreateEventTypeAsync(Client, "BadRangeEvt");
		var academicYearId = Factory.SeededAcademicYearId;

		var input = new CreateUpdateEventDto
		{
			AcademicYearId = academicYearId,
			Title = TestDataHelper.UniqueName("BadRangeEvt"),
			EventTypeId = eventType.Id,
			Mode = EventMode.Online,
			StartDate = DateTimeOffset.UtcNow.AddDays(5),
			EndDate = DateTimeOffset.UtcNow.AddDays(3),
			StartTime = "09:00",
			EndTime = "10:00",
			IsRecurring = false,
			AudienceIds = new List<Guid>(),
		};

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}?academicYearId={academicYearId}", input, JsonOptions);

		response.IsSuccessStatusCode.ShouldBeFalse();
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
			StartDate = DateTimeOffset.UtcNow.AddDays(2),
			EndDate = DateTimeOffset.UtcNow.AddDays(2),
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

		var response = await Client.GetAsync(
			$"{BaseUrl}/by-type/{created.EventTypeId}?SkipCount=0&MaxResultCount=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<PagedResultDto<EventDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.Any(x => x.Id == created.Id).ShouldBeTrue();
		body.Items.All(x => x.EventTypeId == created.EventTypeId).ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Events_By_Status()
	{
		var active = await CreateEventAsync("ByStatusEvt");

		var response = await Client.GetAsync($"{BaseUrl}/by-status/1?SkipCount=0&MaxResultCount=50");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<PagedResultDto<EventDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.Any(x => x.Id == active.Id).ShouldBeTrue();
		body.Items.All(x => x.Status == 1).ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Clear_Stale_Event_Details_On_Update()
	{
		var created = await CreateEventAsync("ClearEvt");
		var updateDto = new CreateUpdateEventDto
		{
			AcademicYearId = created.AcademicYearId,
			Title = TestDataHelper.UniqueName("ClearedEvt"),
			EventTypeId = created.EventTypeId,
			Description = string.Empty,
			Poster = string.Empty,
			Mode = EventMode.Online,
			Venue = null,
			StartDate = created.StartDate,
			EndDate = created.EndDate,
			StartTime = "10:00",
			EndTime = "12:00",
			IsRecurring = false,
			RecurrenceJson = null,
			AudienceIds = new List<Guid>(),
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", updateDto, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<EventDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Mode.ShouldBe(EventMode.Online);
		body.Venue.ShouldBeNull();
		body.Description.ShouldBe(string.Empty);
		body.Poster.ShouldBe(string.Empty);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Filter_Event_List_By_Status()
	{
		var active = await CreateEventAsync("ActiveStatusEvt");

		var response = await Client.GetAsync(
			$"{BaseUrl}?AcademicYearId={Factory.SeededAcademicYearId}&Status=1&SkipCount=0&MaxResultCount=50");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<PagedResultDto<EventDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.Any(x => x.Id == active.Id).ShouldBeTrue();
		body.Items.All(x => x.Status == 1).ShouldBeTrue();
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

	private async Task<EventDto> CreateEventAsync(string suffix, DateTimeOffset? meetingDate = null)
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
			StartDate = meetingDate ?? DateTimeOffset.UtcNow,
			EndDate = meetingDate ?? DateTimeOffset.UtcNow,
			StartTime = "09:00",
			EndTime = "17:00",
			Description = "Original description",
			Poster = "poster-a.png",
			MeetingDate = meetingDate,
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
