using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Academic.Mobile;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Academic.Dashboard;

public class AcademicInsightsCalendarPreview_Tests
{
	[Theory]
	[InlineData(0)]
	[InlineData(7)]
	public void Personal_Calendar_Selects_Five_Real_Events_From_The_Full_Verified_Month(int eventCount)
	{
		var today = new DateOnly(2026, 11, 1);
		MobileSchoolMarkerDto Marker(string title, DateOnly date, string type, string? description = null) =>
			new(Guid.NewGuid(), type, date, title, description, "school", null, null, null, null, type == "holiday");
		var markers = new List<MobileSchoolMarkerDto>();
		for (var i = 0; i < 12; i++) markers.Add(Marker("Sunday Weekend", today.AddDays(i * 7), "holiday", "System generated weekend holiday"));
		markers.Add(Marker("Past event", today.AddDays(-1), "event"));
		for (var i = 0; i < eventCount; i++) markers.Add(Marker(i == 0 ? "Weekend Science Fair" : $"Event {i}",
			today.AddDays(21 + i), i == 1 ? "holiday" : "event"));
		var result = AcademicInsightsCalendarPreview.Personal(markers.AsEnumerable().Reverse(), today);
		result.Status.ShouldBe(eventCount == 0 ? "empty" : "ready");
		result.Items.Count.ShouldBe(Math.Min(eventCount, 5));
		if (eventCount > 0) result.Items.Select(x => x.Label).ShouldBe(["Weekend Science Fair", "Event 1", "Event 2", "Event 3", "Event 4"]);
	}
}
