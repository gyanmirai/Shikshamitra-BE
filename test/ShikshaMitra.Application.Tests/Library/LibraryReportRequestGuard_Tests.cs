using System;
using System.Collections.Generic;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Library.Reports;

public class LibraryReportRequestGuard_Tests
{
	private readonly LibraryReportRequestGuard _guard = new();

	[Fact]
	public void Should_Normalize_A_Valid_Dynamic_Request()
	{
		var request = new LibraryReportRequestDto
		{
			ReportType = LibraryReportTypes.AvailabilityStatus,
			Format = " PDF ",
			Page = 0,
			PageSize = 0,
			Columns = ["title", "status", "title"],
			Sorts = [new LibraryReportSortDto { Field = "title", Direction = " DESC " }],
			Filters = new LibraryReportFilterDto { Search = "  physics  " }
		};

		_guard.ValidateAndNormalize(request);

		request.Format.ShouldBe(LibraryReportFormat.Pdf);
		request.Page.ShouldBe(1);
		request.PageSize.ShouldBe(50);
		request.Columns.ShouldBe(["title", "status"]);
		request.Sorts![0].Direction.ShouldBe("desc");
		request.Filters.Search.ShouldBe("physics");
	}

	[Theory]
	[InlineData("")]
	[InlineData("CSV")]
	public void Should_Reject_Unsupported_Format(string format)
	{
		Should.Throw<BusinessException>(() => _guard.ValidateAndNormalize(Request(format: format)))
			.Code.ShouldBe(LibraryErrorCodes.ReportRequestInvalid);
	}

	[Fact]
	public void Should_Reject_Unsupported_Column()
	{
		var request = Request();
		request.Columns = ["databasePassword"];

		Should.Throw<BusinessException>(() => _guard.ValidateAndNormalize(request))
			.Code.ShouldBe(LibraryErrorCodes.ReportRequestInvalid);
	}

	[Fact]
	public void Should_Reject_Unsupported_Sort_Field()
	{
		var request = Request();
		request.Sorts = [new LibraryReportSortDto { Field = "sql", Direction = "asc" }];

		Should.Throw<BusinessException>(() => _guard.ValidateAndNormalize(request))
			.Code.ShouldBe(LibraryErrorCodes.ReportRequestInvalid);
	}

	[Fact]
	public void Should_Reject_Reversed_Date_Range()
	{
		var request = Request(LibraryReportTypes.IssueReturn);
		request.Filters.FromDate = new DateTime(2026, 9, 2);
		request.Filters.ToDate = new DateTime(2026, 9, 1);

		Should.Throw<BusinessException>(() => _guard.ValidateAndNormalize(request))
			.Code.ShouldBe(LibraryErrorCodes.ReportRequestInvalid);
	}

	[Fact]
	public void Should_Reject_Filter_Not_Supported_By_Report()
	{
		var request = Request(LibraryReportTypes.BookMaster);
		request.Filters.MemberRole = 0;

		Should.Throw<BusinessException>(() => _guard.ValidateAndNormalize(request))
			.Code.ShouldBe(LibraryErrorCodes.ReportRequestInvalid);
	}

	[Fact]
	public void Should_Reject_Invalid_Status()
	{
		var request = Request(LibraryReportTypes.AvailabilityStatus);
		request.Filters.CopyStatuses = [999];

		Should.Throw<BusinessException>(() => _guard.ValidateAndNormalize(request))
			.Code.ShouldBe(LibraryErrorCodes.ReportRequestInvalid);
	}

	[Fact]
	public void Should_Reject_Empty_Identifiers()
	{
		var request = Request();
		request.Filters.CategoryIds = [Guid.Empty];

		Should.Throw<BusinessException>(() => _guard.ValidateAndNormalize(request))
			.Code.ShouldBe(LibraryErrorCodes.ReportRequestInvalid);
	}

	private static LibraryReportRequestDto Request(
		string reportType = LibraryReportTypes.BookMaster,
		string format = LibraryReportFormat.Excel) => new()
	{
		ReportType = reportType,
		Format = format,
		Filters = new LibraryReportFilterDto()
	};
}
