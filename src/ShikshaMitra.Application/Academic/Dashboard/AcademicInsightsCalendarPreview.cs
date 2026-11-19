using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Academic.Mobile;

namespace ShikshaMitra.Academic.Dashboard;

internal static class AcademicInsightsCalendarPreview
{
	internal const string GeneratedWeekendDescription = "System generated weekend holiday";

	internal static AcademicInsightSectionDto Personal(IEnumerable<MobileSchoolMarkerDto> markers, DateOnly today)
	{
		var items = markers.Where(x => x.Date >= today && !(x.Type == "holiday" && x.Description == GeneratedWeekendDescription))
			.OrderBy(x => x.Date).ThenBy(x => x.Id).Take(5)
			.Select(x => new AcademicInsightItemDto(x.Id.ToString(), x.Title, x.Type, x.Date.ToDateTime(TimeOnly.MinValue))).ToList();
		return new() { Key = "calendar", Status = items.Count == 0 ? "empty" : "ready",
			Summary = "Upcoming events this month from the verified portal, excluding recurring weekend holidays; showing up to five.", Items = items };
	}
}
