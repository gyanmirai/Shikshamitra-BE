using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Calendar.Events;

internal sealed record EventDetails(
	string Title,
	DateTimeOffset? MeetingDate,
	DateTimeOffset? StartDate,
	DateTimeOffset? EndDate,
	string StartTime,
	string EndTime,
	string? Description,
	string? Poster,
	EventMode? Mode,
	string? Venue,
	bool? IsRecurring,
	string? RecurrenceJson,
	List<Guid>? AudienceIds);
