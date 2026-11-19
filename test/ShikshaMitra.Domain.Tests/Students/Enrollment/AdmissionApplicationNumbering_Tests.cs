using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionApplicationNumbering_Tests
{
	private static AdmissionApplicationNumberingRule Rule(AdmissionNumberYearFormat year = AdmissionNumberYearFormat.AcademicYear,
		AdmissionNumberResetPolicy reset = AdmissionNumberResetPolicy.AcademicYear, string separator = "/", string suffix = "", int digits = 5, long start = 1) =>
		new(Guid.NewGuid(), Guid.NewGuid(), 1, "ddvs", separator, suffix, year, reset, digits, start);
	[Fact]
	public void Format_uses_admission_academic_year_and_normalizes_school_text()
	{
		var rule = Rule(suffix: "a");
		rule.Format(1, rule.YearText(2026, 2027, 2028)).ShouldBe("DDVS/2027-28/00001/A");
		rule.Format(123456, "2027-28").ShouldBe("DDVS/2027-28/123456/A");
	}
	[Theory]
	[InlineData(AdmissionNumberYearFormat.None, AdmissionNumberResetPolicy.Continuous, "DDVS/00001")]
	[InlineData(AdmissionNumberYearFormat.CalendarYear, AdmissionNumberResetPolicy.CalendarYear, "DDVS/2026/00001")]
	public void Supports_continuous_and_calendar_formats(AdmissionNumberYearFormat year, AdmissionNumberResetPolicy reset, string expected)
	{ var rule = Rule(year, reset); rule.Format(1, rule.YearText(2026, 2027, 2028)).ShouldBe(expected); }
	[Theory]
	[InlineData(AdmissionNumberYearFormat.None, AdmissionNumberResetPolicy.AcademicYear)]
	[InlineData(AdmissionNumberYearFormat.CalendarYear, AdmissionNumberResetPolicy.AcademicYear)]
	[InlineData(AdmissionNumberYearFormat.AcademicYear, AdmissionNumberResetPolicy.CalendarYear)]
	public void Rejects_reset_without_matching_year(AdmissionNumberYearFormat year, AdmissionNumberResetPolicy reset) =>
		Should.Throw<BusinessException>(() => Rule(year, reset)).Code.ShouldBe(AdmissionNumberingErrorCodes.Invalid);
	[Theory]
	[InlineData(0, 1)] [InlineData(13, 1)] [InlineData(5, 0)] [InlineData(5, 1000000000)]
	public void Rejects_invalid_ranges(int digits, long start) => Should.Throw<BusinessException>(() => Rule(digits: digits, start: start));
	[Fact]
	public void Reads_only_exact_issued_numeric_format()
	{
		var rule = Rule();
		rule.TryReadSequence("DDVS/2026-27/00042", "2026-27", out var n).ShouldBeTrue(); n.ShouldBe(42);
		rule.TryReadSequence("DDVS/2026-27/42oops", "2026-27", out _).ShouldBeFalse();
		rule.TryReadSequence("DDVS/2027-28/00042", "2026-27", out _).ShouldBeFalse();
	}
	[Fact]
	public void Counter_floor_never_rewinds_and_academic_scopes_are_distinct()
	{
		var rule = Rule(); var year = Guid.NewGuid();
		rule.ScopeKey(2026, year).ShouldNotBe(rule.ScopeKey(2026, Guid.NewGuid()));
		var counter = new AdmissionApplicationNumberCounter(Guid.NewGuid(), null, "continuous", 40);
		counter.TakeNext().ShouldBe(40); counter.EnsureFloor(1, 2, "2026-27"); counter.TakeNext().ShouldBe(41);
		counter.EnsureFloor(100, 3, "2027-28"); counter.TakeNext().ShouldBe(100);
	}
}
