using System;
using ShikshaMitra.HR.Policies;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Policies;

public class HrSetupReviewHolidayFingerprint_Tests
{
	[Theory]
	[InlineData("year"), InlineData("calendar"), InlineData("end"), InlineData("source"), InlineData("included"), InlineData("notes")]
	public void Reversed_tied_rows_have_identical_hashes_and_every_stable_value_still_matters(string difference)
	{
		var tenant = Guid.NewGuid(); var year = Guid.NewGuid(); var calendar = Guid.NewGuid();
		var date = new DateOnly(2026, 6, 10);
		var first = new WorkCalendarHoliday(Guid.NewGuid(), tenant, year, calendar, date, date,
			"Tied manual row", WorkCalendarHolidaySource.Manual, null, false, "A");
		var second = new WorkCalendarHoliday(Guid.NewGuid(), tenant,
			difference == "year" ? Guid.NewGuid() : year,
			difference == "calendar" ? Guid.NewGuid() : calendar,
			date, difference == "end" ? date.AddDays(1) : date, "Tied manual row",
			difference == "source" ? WorkCalendarHolidaySource.CentralCalendar : WorkCalendarHolidaySource.Manual,
			null, difference == "included", difference == "notes" ? "B" : "A");
		var forward = HrSetupReviewConfigurationReader.Hash(HrSetupReviewConfigurationReader.CanonicalLinkedHolidayValues([first, second]));
		var reversed = HrSetupReviewConfigurationReader.Hash(HrSetupReviewConfigurationReader.CanonicalLinkedHolidayValues([second, first]));
		reversed.ShouldBe(forward);
		// End-date and note cases represent legal excluded manual rows with identical original sort keys.
		// Regenerated preview-only IDs and row enumeration must not create false staleness.
		var newIds = HrSetupReviewConfigurationReader.Hash(HrSetupReviewConfigurationReader.CanonicalLinkedHolidayValues([Copy(second), Copy(first)]));
		newIds.ShouldBe(forward);
		var unchangedValues = HrSetupReviewConfigurationReader.Hash(HrSetupReviewConfigurationReader.CanonicalLinkedHolidayValues([first, Copy(first)]));
		unchangedValues.ShouldNotBe(forward);
	}

	private static WorkCalendarHoliday Copy(WorkCalendarHoliday row) => new(Guid.NewGuid(), row.TenantId,
		row.AcademicYearId, row.WorkCalendarPolicyId, row.FromDate, row.ToDate, row.HolidayName, row.Source,
		row.CalendarHolidayId, row.IsIncluded, row.Notes);
}
