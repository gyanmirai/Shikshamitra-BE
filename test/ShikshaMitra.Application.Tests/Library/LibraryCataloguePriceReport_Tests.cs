using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Reports;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	[Fact]
	public async Task Catalogue_Price_Report_Should_Preserve_Unknown_Zero_And_Full_Precision_Without_A_Currency_Inference()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var unknown = await SeedOperationalBookAsync(fixture, "Unknown catalogue price", fixture.Context.Catalog.OriginYearId, false);
		var free = await SeedOperationalBookAsync(fixture, "Known free catalogue price", fixture.Context.Catalog.OriginYearId, false);
		var exact = await SeedOperationalBookAsync(fixture, "Exact catalogue price", fixture.Context.Catalog.OriginYearId, false);
		await WithUnitOfWorkAsync(async () =>
		{
			var books = GetRequiredService<IRepository<Book, Guid>>();
			foreach (var (id, price) in new (Guid, decimal?)[]
				{ (unknown.Book.Id, null), (free.Book.Id, 0m), (exact.Book.Id, 9999999999999999.99m) })
			{
				var book = await books.GetAsync(id);
				book.Price = price;
				await books.UpdateAsync(book, true);
			}
		});
		var report = await ReadOrderedReportAsync(new()
		{
			ReportType = LibraryReportTypes.BookMaster, PageSize = 20,
			Filters = new() { BookIds = [unknown.Book.Id, free.Book.Id, exact.Book.Id] }
		});
		report.TotalCount.ShouldBe(3);
		report.Rows.Single(row => row["bookId"] == unknown.Book.Id.ToString())["price"].ShouldBeNull();
		report.Rows.Single(row => row["bookId"] == free.Book.Id.ToString())["price"].ShouldBe("0.00");
		report.Rows.Single(row => row["bookId"] == exact.Book.Id.ToString())["price"].ShouldBe("9999999999999999.99");
		using var workbook = new XLWorkbook(new MemoryStream(GetRequiredService<LibraryExcelService>().GenerateReportExcel(report)));
		var priceColumn = report.Columns.Select((column, index) => new { column.Id, Number = index + 1 }).Single(column => column.Id == "price").Number;
		for (var index = 0; index < report.Rows.Count; index++)
			workbook.Worksheet(1).Cell(index + 2, priceColumn).GetString().ShouldBe(report.Rows[index]["price"] ?? "");
	}
}
