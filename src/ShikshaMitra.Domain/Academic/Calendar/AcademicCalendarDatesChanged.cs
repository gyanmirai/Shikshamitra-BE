using System;

namespace ShikshaMitra.Academic.Calendar;

public sealed record AcademicCalendarDatesChanged(
	Guid CalendarId, string CalendarName,
	DateTime PreviousStartDate, DateTime PreviousEndDate,
	DateTime StartDate, DateTime EndDate);
