namespace ShikshaMitra.HR.Calendar.Events;

internal static class EventDetailsMutator
{
	public static void Apply(Event @event, EventDetails details)
	{
		@event.Title = details.Title;
		@event.MeetingDate = details.MeetingDate;
		@event.StartTime = details.StartTime;
		@event.EndTime = details.EndTime;

		if (details.Description != null)
		{
			@event.Description = details.Description;
		}

		if (details.Poster != null)
		{
			@event.Poster = details.Poster;
		}

		if (details.Mode.HasValue)
		{
			@event.Mode = details.Mode.Value;
		}

		if (details.Venue != null)
		{
			@event.Venue = details.Venue;
		}

		if (details.IsRecurring.HasValue)
		{
			@event.IsRecurring = details.IsRecurring.Value;
		}

		if (details.RecurrenceJson != null)
		{
			@event.RecurrenceJson = details.RecurrenceJson;
		}

		if (details.AudienceIds != null)
		{
			@event.AudienceIds = details.AudienceIds;
		}
	}
}
