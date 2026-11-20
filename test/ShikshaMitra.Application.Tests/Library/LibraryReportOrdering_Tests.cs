using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Reports;
using ShikshaMitra.Library.Shared;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	[Fact]
	public async Task H_ReportOrdering_Should_Sort_Physical_Copy_Counts_Numerically_Before_Paging()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var ten = await SeedOperationalBookAsync(fixture, "Ten physical copies", fixture.Context.Catalog.OriginYearId, false);
		var two = await SeedOperationalBookAsync(fixture, "Two physical copies", fixture.Context.Catalog.OriginYearId, false);
		await WithUnitOfWorkAsync(async () =>
		{
			var copies = GetRequiredService<IRepository<BookCopy, Guid>>();
			foreach (var (book, count) in new[] { (ten.Book, 10), (two.Book, 2) })
				for (var number = 1; number <= count; number++)
					await copies.InsertAsync(new(Guid.NewGuid(), book.Id, number, $"SORT-{Guid.NewGuid():N}",
						fixture.Context.Catalog.SectionId, fixture.Context.Catalog.ShelfId, book.TenantId), true);
		});
		var report = await ReadOrderedReportAsync(new()
		{
			ReportType = LibraryReportTypes.BookMaster, PageSize = 20, Filters = new() { BookIds = [ten.Book.Id, two.Book.Id] },
			Sorts = [new() { Field = "totalCopies", Direction = "asc" }]
		});
		report.TotalCount.ShouldBe(2);
		report.Rows.Select(row => row["totalCopies"]).Order().ShouldBe(["10", "2"]);

		report.Rows.Select(row => row["title"]).ShouldBe([two.Book.Title, ten.Book.Title]);
	}

	[Fact]
	public async Task H_ReportOrdering_Accepted_MixedCase_Legacy_And_School_Columns_And_Sorts_Should_Resolve_Actual_Values()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var first = await SeedOperationalBookAsync(fixture, "Earlier reviewed loan", fixture.Context.Catalog.OriginYearId, true);
		var second = await SeedOperationalBookAsync(fixture, "Later reviewed loan", fixture.Context.Catalog.OriginYearId, true);
		await SeedOrderedReviewedLoanAsync(fixture, first.Copy!, new(2026, 11, 10), new(2001, 1, 2));
		await SeedOrderedReviewedLoanAsync(fixture, second.Copy!, new(2026, 11, 17), new(2001, 1, 3));
		var request = new LibraryReportRequestDto
		{
			ReportType = LibraryReportTypes.IssueReturn, PageSize = 20,
			Columns = ["TITLE", "DUEdate", "SCHOOLissueDATE"],
			Sorts = [new() { Field = "SCHOOLissueDATE", Direction = "desc" }],
			Filters = new() { BookIds = [first.Book.Id, second.Book.Id] }
		};
		var report = await ReadOrderedReportAsync(request);
		report.Rows.Count.ShouldBe(2);
		report.Rows.All(row => !string.IsNullOrEmpty(row["dueDate"]) && !string.IsNullOrEmpty(row["schoolIssueDate"])).ShouldBeTrue();

		report.Columns.Select(column => column.Id).ShouldBe(["title", "dueDate", "schoolIssueDate"]);
		request.Sorts.Single().Field.ShouldBe("schoolIssueDate");
		report.Rows.Select(row => row[report.Columns[0].Id]).ShouldBe([second.Book.Title, first.Book.Title]);
		report.Rows.Select(row => row[report.Columns[1].Id]).ShouldBe(["2001-01-03", "2001-01-02"]);
	}

	[Fact]
	public async Task H_ReportOrdering_An_Extreme_Valid_Page_Should_Be_Empty_Without_Offset_Overflow()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var report = await ReadOrderedReportAsync(new()
		{
			ReportType = LibraryReportTypes.BookMaster, Page = int.MaxValue, PageSize = 500,
			Filters = new() { BookIds = [fixture.Context.Catalog.BookId] }
		});
		report.TotalCount.ShouldBe(1);
		report.Page.ShouldBe(int.MaxValue);

		report.Rows.ShouldBeEmpty();
	}

	private async Task<LibraryReportPreviewDto> ReadOrderedReportAsync(LibraryReportRequestDto request)
	{
		new LibraryReportRequestGuard().ValidateAndNormalize(request);
		var context = await GetRequiredService<ILibraryAccessService>().GetContextAsync(request.Filters.AcademicYearId);
		return await WithUnitOfWorkAsync(() => GetRequiredService<LibraryReportGenerator>().GenerateAsync(request,
			new() { TenantId = context.UserScope.TenantId, UserScope = context.UserScope,
				AllowedAcademicYearIds = context.AllowedAcademicYearIds }, exportAllRows: false));
	}

	private Task SeedOrderedReviewedLoanAsync(StaffScopeFixture fixture, BookCopy copy, DateOnly issue, DateTime rawDue) => WithUnitOfWorkAsync(async () =>
	{
		var loan = new BookIssue(Guid.NewGuid(), copy.Id, copy.AccessionNumber, fixture.Context.StudentId, 0,
			new(2001, 1, 1), rawDue, copy.TenantId);
		loan.ReviewSchoolDates(issue, new(2026, 11, 24), new(2026, 11, 24), null);
		await GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(loan, true);
		copy.Status = BookCopyStatus.Issued;
		await GetRequiredService<IRepository<BookCopy, Guid>>().UpdateAsync(copy, true);
	});
}
