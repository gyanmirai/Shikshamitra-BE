using System;
using ShikshaMitra.Library.Reports;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryReportPresentationGuard_Tests
{
	[Fact]
	public void H_ReportPresentation_Canonical_Columns_Should_Be_Accepted_By_The_Owning_Request_Guard()
	{
		var request = new LibraryReportRequestDto
		{
			ReportType = LibraryReportTypes.IssueReturn, Columns = ["schoolIssueDate", "schoolDueDate", "schoolReturnDate", "dateEvidence"]
		};
		Should.NotThrow(() => new LibraryReportRequestGuard().ValidateAndNormalize(request));
	}

	[Theory]
	[InlineData(LibraryReportTypes.BookMaster)]
	[InlineData(LibraryReportTypes.StockSummary)]
	public void H_ReportPresentation_Should_Reject_An_AsOf_Filter_On_An_Unclassified_Inventory_Report(string reportType)
	{
		var request = new LibraryReportRequestDto { ReportType = reportType, Filters = new() { AsOfDate = new(2026, 11, 18) } };
		Should.Throw<BusinessException>(() => new LibraryReportRequestGuard().ValidateAndNormalize(request))
			.Code.ShouldBe(LibraryErrorCodes.ReportRequestInvalid);
	}

	[Fact]
	public void H_ReportPresentation_Should_Preserve_An_Explicit_AsOf_Date_For_An_Overdue_Report()
	{
		var request = new LibraryReportRequestDto { ReportType = LibraryReportTypes.OverdueBooks, Filters = new() { AsOfDate = new(2026, 11, 18) } };
		Should.NotThrow(() => new LibraryReportRequestGuard().ValidateAndNormalize(request));
		request.Filters.AsOfDate.ShouldBe(new DateOnly(2026, 11, 18));
	}
}
