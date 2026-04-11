using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Calendar.EventTypes;
using ShikshaMitra.Infrastructure.Data;

namespace ShikshaMitra.HR.Calendar;

/// <summary>
/// Shared helper for creating calendar-related test entities via the API.
/// </summary>
public static class CalendarTestHelper
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static async Task<EventTypeDto> CreateEventTypeAsync(
		HttpClient client, string? nameSuffix = null)
	{
		var input = new CreateUpdateEventTypeDto
		{
			Name = TestDataHelper.UniqueName(nameSuffix ?? "EvtType"),
			Color = "#FF5733",
			Description = "Test event type",
		};

		var response = await client.PostAsJsonAsync("/api/calendar/event-types", input, JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<EventTypeDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create event type");
	}

	public static async Task<HolidayTypeDto> CreateHolidayTypeAsync(
		HttpClient client, string? nameSuffix = null)
	{
		var input = new CreateUpdateHolidayTypeDto
		{
			Name = TestDataHelper.UniqueName(nameSuffix ?? "HolType"),
			Color = "#00BFFF",
			Description = "Test holiday type",
		};

		var response = await client.PostAsJsonAsync("/api/calendar/holiday-types", input, JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<HolidayTypeDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create holiday type");
	}
}
