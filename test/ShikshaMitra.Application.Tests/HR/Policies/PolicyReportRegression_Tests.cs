using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Reports;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Policies;

public class PolicyReportRegression_Tests
{
	[Fact]
	public void WorkCalendarAudit_Should_Include_Only_Changed_Holidays_And_Real_Summary_Values()
	{
		var unchangedId = Guid.NewGuid();
		var changedId = Guid.NewGuid();
		var before = BuildCalendar(
			new WorkCalendarHolidaySummaryDto
			{
				TotalCount = 2,
				IncludedCount = 2,
				CentralCount = 2
			},
			BuildHoliday(unchangedId, "Founders Day", true),
			BuildHoliday(changedId, "Local Holiday", true));
		var after = BuildCalendar(
			new WorkCalendarHolidaySummaryDto
			{
				TotalCount = 2,
				IncludedCount = 1,
				RemovedCount = 1,
				CentralCount = 2
			},
			BuildHoliday(unchangedId, "Founders Day", true),
			BuildHoliday(changedId, "Local Holiday", false));

		var changes = WorkCalendarPolicyAuditChangeBuilder.Build(before, after);

		var holidayChange = changes.Single(x => x.Field == "holidays");
		using var oldHolidays = JsonDocument.Parse(holidayChange.OldValue!);
		using var newHolidays = JsonDocument.Parse(holidayChange.NewValue!);
		oldHolidays.RootElement.GetArrayLength().ShouldBe(1);
		newHolidays.RootElement.GetArrayLength().ShouldBe(1);
		oldHolidays.RootElement[0].GetProperty("holidayName").GetString().ShouldBe("Local Holiday");
		newHolidays.RootElement[0].GetProperty("isIncluded").GetBoolean().ShouldBeFalse();

		var summaryChange = changes.Single(x => x.Field == "holidaySummary");
		using var oldSummary = JsonDocument.Parse(summaryChange.OldValue!);
		using var newSummary = JsonDocument.Parse(summaryChange.NewValue!);
		oldSummary.RootElement.GetProperty("includedCount").GetInt32().ShouldBe(2);
		newSummary.RootElement.GetProperty("removedCount").GetInt32().ShouldBe(1);
	}

	[Fact]
	public void WorkCalendarAudit_Should_Tolerate_Duplicate_Legacy_Holiday_Keys()
	{
		var calendarHolidayId = Guid.NewGuid();
		var before = BuildCalendar(
			new WorkCalendarHolidaySummaryDto { TotalCount = 2, IncludedCount = 2 },
			BuildHoliday(calendarHolidayId, "Legacy Holiday", true),
			BuildHoliday(calendarHolidayId, "Legacy Holiday", true));
		var after = BuildCalendar(
			new WorkCalendarHolidaySummaryDto { TotalCount = 1, IncludedCount = 1 },
			BuildHoliday(calendarHolidayId, "Legacy Holiday", true));

		var exception = Record.Exception(() => WorkCalendarPolicyAuditChangeBuilder.Build(before, after));

		exception.ShouldBeNull();
	}

	[Fact]
	public void WorkCalendarAudit_Should_Not_Report_Unchanged_Manual_Holiday_When_Snapshot_Id_Changes()
	{
		var before = BuildCalendar(
			new WorkCalendarHolidaySummaryDto { TotalCount = 1, IncludedCount = 1, ManualCount = 1 },
			BuildManualHoliday(Guid.NewGuid(), "Local Foundation Day", true));
		var after = BuildCalendar(
			new WorkCalendarHolidaySummaryDto { TotalCount = 1, IncludedCount = 1, ManualCount = 1 },
			BuildManualHoliday(Guid.NewGuid(), "Local Foundation Day", true));
		after.Name = "Renamed calendar";

		var changes = WorkCalendarPolicyAuditChangeBuilder.Build(before, after);

		changes.ShouldContain(change => change.Field == "name");
		changes.ShouldNotContain(change => change.Field == "holidays");
		changes.ShouldNotContain(change => change.Field == "holidaySummary");
	}

	[Fact]
	public void VersionDiff_Should_Ignore_Equivalent_Effective_Dates()
	{
		const string before = """{"effectiveFrom":"2026-04-01T00:00:00Z","name":"Policy"}""";
		const string after = """{"effectiveFrom":"20260401T000000Z","name":"Policy"}""";

		var changes = HrPolicyVersionDiffBuilder.BuildDifferences(
			before,
			after,
			new HrPolicyVersionDisplayContext());

		changes.ShouldBeEmpty();
	}

	[Fact]
	public void HolidaySummary_Display_Should_Expose_Counts_Instead_Of_Generic_Changed_Text()
	{
		using var document = JsonDocument.Parse(
			"""{"totalCount":5,"includedCount":3,"removedCount":2,"centralCount":4,"manualCount":1}""");

		var display = HrPolicyVersionDisplayFormatter.FormatDisplayValue(
			"holidaySummary",
			document.RootElement,
			new HrPolicyVersionDisplayContext());

		display.ShouldContain("Total: 5");
		display.ShouldContain("Included: 3");
		display.ShouldContain("Removed: 2");
		display.ShouldNotBe("Changed");
	}

	[Fact]
	public async Task PdfService_Should_Generate_Wide_Report_Without_Layout_Exception()
	{
		var columns = Enumerable.Range(1, 25)
			.Select(index => new HrReportColumnDto
			{
				Id = $"column{index}",
				Label = $"Column {index}"
			})
			.ToList();
		var row = columns.ToDictionary(
			column => column.Id,
			column => (string?)$"<strong>{column.Label}</strong> value with enough content to wrap safely");
		var report = new HrReportPreviewDto
		{
			DataSource = HrReportDataSources.Employee,
			ReportType = "Wide employee report",
			TotalCount = 1,
			Page = 1,
			PageSize = 50,
			Columns = columns,
			Rows = [row]
		};

		var pdf = await new HRPdfService().GenerateReportPdf(report);

		pdf.Length.ShouldBeGreaterThan(1_000);
		Encoding.ASCII.GetString(pdf, 0, 4).ShouldBe("%PDF");
	}

	[Fact]
	public async Task PdfService_Should_Generate_A_Friendly_Pdf_When_No_Columns_Are_Selected()
	{
		var report = new HrReportPreviewDto
		{
			DataSource = HrReportDataSources.Employee,
			ReportType = "Empty column report",
			TotalCount = 1,
			Page = 1,
			PageSize = 50,
			Rows = [new Dictionary<string, string?>()]
		};

		var pdf = await new HRPdfService().GenerateReportPdf(report);

		pdf.Length.ShouldBeGreaterThan(1_000);
		Encoding.ASCII.GetString(pdf, 0, 4).ShouldBe("%PDF");
	}

	[Fact]
	public void PdfService_Should_Report_The_Number_Of_Rows_Actually_Included()
	{
		var report = new HrReportPreviewDto
		{
			DataSource = HrReportDataSources.Employee,
			ReportType = "Large employee report",
			TotalCount = 1_000,
			Page = 1,
			PageSize = 1_000,
			Rows = Enumerable.Range(1, 750)
				.Select(_ => new Dictionary<string, string?>())
				.ToList()
		};

		HRPdfService.GetIncludedRowCount(report).ShouldBe(HRPdfService.MaxPdfRows);
	}

	[Fact]
	public void ExportErrorFormatter_Should_Not_Expose_Technical_Pdf_Failures()
	{
		var message = HrReportExportErrorFormatter.NormalizePersistedMessage(
			"Conflicting size constraints detected in QuestPDF row element.",
			HrReportFormat.Pdf);

		message.ShouldStartWith("PDF export could not be generated");
		message.ShouldNotContain("QuestPDF");
	}

	private static WorkCalendarPolicyDto BuildCalendar(
		WorkCalendarHolidaySummaryDto summary,
		params WorkCalendarHolidayDto[] holidays) =>
		new()
		{
			Id = Guid.NewGuid(),
			AcademicYearId = Guid.NewGuid(),
			Name = "Calendar",
			Status = "active",
			WeeklyOffPattern = "sunday",
			CustomWeeklyOffs = [0],
			Holidays = [.. holidays],
			HolidaySummary = summary
		};

	private static WorkCalendarHolidayDto BuildHoliday(
		Guid calendarHolidayId,
		string name,
		bool isIncluded) =>
		new()
		{
			AcademicYearId = Guid.NewGuid(),
			CalendarHolidayId = calendarHolidayId,
			HolidayName = name,
			Date = new DateOnly(2026, 5, 1),
			Source = "central_calendar",
			IsIncluded = isIncluded
		};

	private static WorkCalendarHolidayDto BuildManualHoliday(
		Guid snapshotId,
		string name,
		bool isIncluded) =>
		new()
		{
			Id = snapshotId,
			AcademicYearId = Guid.NewGuid(),
			HolidayName = name,
			Date = new DateOnly(2026, 5, 1),
			Source = "manual",
			IsIncluded = isIncluded
		};
}
