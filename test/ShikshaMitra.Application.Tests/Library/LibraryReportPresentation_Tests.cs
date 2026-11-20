using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Reports;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	[Fact]
	public async Task H_ReportPresentation_Default_Columns_Should_Show_School_Dates_And_Their_Evidence()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		await SeedStatisticsLoanAsync(fixture, new(2026, 11, 17));
		var report = await ReadOperationalReportAsync(LibraryReportTypes.IssueReturn);
		report.Rows.Single()["schoolDueDate"].ShouldBe("2026-11-17");

		report.Columns.Select(column => column.Id).ShouldContain("schoolIssueDate");
		report.Columns.Select(column => column.Id).ShouldContain("schoolDueDate");
		report.Columns.Select(column => column.Id).ShouldContain("schoolReturnDate");
		report.Columns.Select(column => column.Id).ShouldContain("dateEvidence");
	}

	[Fact]
	public async Task H_ReportPresentation_Legacy_Columns_Should_Remain_Requestable_With_Explicit_Component_Labels()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		await SeedStaffAsync();
		var request = new LibraryReportRequestDto
		{
			ReportType = LibraryReportTypes.IssueReturn, Columns = ["issueDate", "dueDate", "returnDate"]
		};
		Should.NotThrow(() => new LibraryReportRequestGuard().ValidateAndNormalize(request));
		var context = await GetRequiredService<Shared.ILibraryAccessService>().GetContextAsync();
		var report = await WithUnitOfWorkAsync(() => GetRequiredService<LibraryReportGenerator>().GenerateAsync(request,
			new() { TenantId = context.UserScope.TenantId, UserScope = context.UserScope,
				AllowedAcademicYearIds = context.AllowedAcademicYearIds }, exportAllRows: false));

		report.Columns.Single(column => column.Id == "issueDate").Label.ShouldBe("Legacy issue date component");
		report.Columns.Single(column => column.Id == "dueDate").Label.ShouldBe("Legacy due date component");
		report.Columns.Single(column => column.Id == "returnDate").Label.ShouldBe("Legacy return date component");
	}
}
