using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ShikshaMitra.Library.Reports;

internal static class LibraryReportDateSummary
{
	public static List<(string Label, string Value)> Read(LibraryReportPreviewDto report)
	{
		var inventory = report.ReportType is LibraryReportTypes.BookMaster or LibraryReportTypes.StockSummary or
			LibraryReportTypes.AvailabilityStatus or LibraryReportTypes.LostDamagedBooks;
		var rows = new List<(string, string)>
		{
			("Report", report.ReportType),
			("Matching records", report.TotalCount.ToString(CultureInfo.InvariantCulture)),
			(inventory ? "Academic year access context ID" : "Recorded academic year filter ID", report.NormalizedFilters.AcademicYearId?.ToString() ??
				(inventory ? "Permanent inventory across academic years" : "All authorized recorded years"))
		};
		if (report.ReportType is LibraryReportTypes.IssueReturn or LibraryReportTypes.OverdueBooks or LibraryReportTypes.MemberList)
		{
			rows.Add((report.SchoolDateSource == "ExplicitSchoolDate" ? "Chosen school date" : "School clock date",
				report.SchoolAsOfDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "Unavailable"));
			rows.Add(("School date source", report.SchoolDateSource));
			rows.Add(("Overdue classification", report.OverdueClassificationAvailable ? "Complete" : "Incomplete or unavailable"));
			rows.Add(("Unknown issue dates", report.UnknownIssueDateCount.ToString(CultureInfo.InvariantCulture)));
			rows.Add(("Unknown due dates", report.UnknownDueDateCount.ToString(CultureInfo.InvariantCulture)));
			rows.Add(("Unknown return dates", report.UnknownReturnDateCount.ToString(CultureInfo.InvariantCulture)));
			if (!string.IsNullOrWhiteSpace(report.SchoolDateUnavailableReason)) rows.Add(("School date review", report.SchoolDateUnavailableReason));
			if (!string.IsNullOrWhiteSpace(report.OverdueUnavailableReason) && report.OverdueUnavailableReason != report.SchoolDateUnavailableReason)
				rows.Add(("Overdue review", report.OverdueUnavailableReason));
		}
		foreach (var warning in report.QueryWarnings.Distinct())
			if (!rows.Any(row => row.Item2 == warning)) rows.Add(("Warning", warning));
		return rows;
	}
}
