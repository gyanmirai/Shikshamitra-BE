namespace ShikshaMitra.HR.Calendar.Events;

internal static class EventDetailsMutator
{
	public static void Apply(Event @event, EventDetails details)
	{
		@event.Title = details.Title;
		@event.MeetingDate = details.MeetingDate;
		@event.StartDate = details.StartDate;
		@event.EndDate = details.EndDate;
		@event.StartTime = details.StartTime;
		@event.EndTime = details.EndTime;
		@event.Description = details.Description;
		@event.Poster = details.Poster;

		if (details.Mode.HasValue)
		{
			@event.Mode = details.Mode.Value;
		}
		@event.Venue = details.Venue;

		if (details.IsRecurring.HasValue)
		{
			@event.IsRecurring = details.IsRecurring.Value;
		}
		@event.RecurrenceJson = details.RecurrenceJson;

		if (details.AudienceIds != null)
		{
			@event.AudienceIds = details.AudienceIds;
		}
	}
}
