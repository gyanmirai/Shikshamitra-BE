using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Catalogue;

namespace ShikshaMitra.Library.Reports;

public partial class LibraryReportGenerator
{
	private static List<Dictionary<string, string?>> BuildBookMasterRows(LibraryReportContext context, LibraryReportFilterDto filters)
	{
		var copiesByBook = context.Copies.GroupBy(x => x.BookId).ToDictionary(x => x.Key, x => x.ToList());
		return context.Books
			.Select(book =>
			{
				var copies = copiesByBook.GetValueOrDefault(book.Id) ?? [];
				return Row(
					("bookId", book.Id.ToString()),
					("title", book.Title),
					("isbn", book.Isbn),
					("author", book.Author),
					("publisher", book.Publisher),
					("edition", book.Edition),
					("subject", book.Subject),
					("category", context.Categories.GetValueOrDefault(book.CategoryId, string.Empty)),
					("language", context.CatalogueLabels.LanguageLabel(book)),
					("price", LibraryCataloguePriceRules.Format(book.Price)),
					("pages", book.Pages.ToString(CultureInfo.InvariantCulture)),
					("totalCopies", copies.Count.ToString(CultureInfo.InvariantCulture)),
					("availableCopies", copies.Count(x => x.Status == BookCopyStatus.Available).ToString(CultureInfo.InvariantCulture)),
					("issuedCopies", copies.Count(x => x.Status == BookCopyStatus.Issued).ToString(CultureInfo.InvariantCulture)),
					("reservedCopies", copies.Count(x => x.Status == BookCopyStatus.Reserved).ToString(CultureInfo.InvariantCulture)),
					("lostCopies", copies.Count(x => x.Status == BookCopyStatus.Lost).ToString(CultureInfo.InvariantCulture)),
					("damagedCopies", copies.Count(x => x.Status == BookCopyStatus.Damaged).ToString(CultureInfo.InvariantCulture)),
					("inCustodyCopies", copies.Count(x => x.Status == BookCopyStatus.InCustody).ToString(CultureInfo.InvariantCulture)),
					("inTransitCopies", copies.Count(x => x.Status == BookCopyStatus.InTransit).ToString(CultureInfo.InvariantCulture)));
			})
			.ToList();
	}

	private static List<Dictionary<string, string?>> BuildStockSummaryRows(LibraryReportContext context, LibraryReportFilterDto filters) =>
		context.Books
			.GroupBy(x => x.CategoryId)
			.Select(group =>
			{
				var bookIds = group.Select(x => x.Id).ToHashSet();
				var copies = context.Copies.Where(x => bookIds.Contains(x.BookId)).ToList();
				return Row(
					("category", context.Categories.GetValueOrDefault(group.Key, "Uncategorized")),
					("bookCount", group.Count().ToString(CultureInfo.InvariantCulture)),
					("totalCopies", copies.Count.ToString(CultureInfo.InvariantCulture)),
					("availableCopies", copies.Count(x => x.Status == BookCopyStatus.Available).ToString(CultureInfo.InvariantCulture)),
					("issuedCopies", copies.Count(x => x.Status == BookCopyStatus.Issued).ToString(CultureInfo.InvariantCulture)),
					("reservedCopies", copies.Count(x => x.Status == BookCopyStatus.Reserved).ToString(CultureInfo.InvariantCulture)),
					("lostCopies", copies.Count(x => x.Status == BookCopyStatus.Lost).ToString(CultureInfo.InvariantCulture)),
					("damagedCopies", copies.Count(x => x.Status == BookCopyStatus.Damaged).ToString(CultureInfo.InvariantCulture)),
					("inCustodyCopies", copies.Count(x => x.Status == BookCopyStatus.InCustody).ToString(CultureInfo.InvariantCulture)),
					("inTransitCopies", copies.Count(x => x.Status == BookCopyStatus.InTransit).ToString(CultureInfo.InvariantCulture)));
			})
			.OrderBy(x => x["category"])
			.ToList();

	private static List<Dictionary<string, string?>> BuildAvailabilityRows(LibraryReportContext context, LibraryReportFilterDto filters) =>
		context.Copies
			.Where(copy => filters.SectionIds is not { Count: > 0 } || filters.SectionIds.Contains(copy.SectionId))
			.Where(copy => filters.CopyStatuses is not { Count: > 0 } || filters.CopyStatuses.Contains((int)copy.Status))
			.Select(copy =>
			{
				var book = context.BookById.GetValueOrDefault(copy.BookId);
				return Row(
					("accessionNumber", copy.AccessionNumber),
					("copyNumber", copy.CopyNumber.ToString(CultureInfo.InvariantCulture)),
					("title", book?.Title),
					("author", book?.Author),
					("category", book == null ? null : context.Categories.GetValueOrDefault(book.CategoryId)),
					("status", LibraryReportCatalog.CopyStatusLabel(copy.Status)),
					("section", context.Sections.GetValueOrDefault(copy.SectionId)),
					("shelf", context.Shelves.GetValueOrDefault(copy.ShelfId)),
					("remarks", copy.Remarks));
			})
			.ToList();

	private static List<Dictionary<string, string?>> BuildLostDamagedRows(LibraryReportContext context, LibraryReportFilterDto filters) =>
		BuildAvailabilityRows(context, new LibraryReportFilterDto
		{
			SectionIds = filters.SectionIds,
			CopyStatuses = filters.CopyStatuses is { Count: > 0 }
				? filters.CopyStatuses
				: [(int)BookCopyStatus.Lost, (int)BookCopyStatus.Damaged],
			Search = filters.Search
		})
			.Where(row => row.GetValueOrDefault("status") is nameof(BookCopyStatus.Lost) or nameof(BookCopyStatus.Damaged))
			.ToList();

	private static List<Dictionary<string, string?>> BuildPopularRows(LibraryReportContext context, LibraryReportFilterDto filters)
	{
		var top = Math.Clamp(filters.Top ?? 10, 1, 100);
		return context.Books
			.Select(book =>
			{
				var issueCount = context.Issues.Count(issue => context.ResolveBook(issue)?.Id == book.Id);
				return Row(
					("title", book.Title),
					("author", book.Author),
					("category", context.Categories.GetValueOrDefault(book.CategoryId)),
					("issueCount", issueCount.ToString(CultureInfo.InvariantCulture)),
					("activeIssues", context.Issues.Count(issue => issue.Status == BookIssueStatus.Issued && context.ResolveBook(issue)?.Id == book.Id).ToString(CultureInfo.InvariantCulture)),
					("reservationCount", context.Reservations.Count(x => x.BookId == book.Id).ToString(CultureInfo.InvariantCulture)));
			})
			.OrderByDescending(x => Number(x.GetValueOrDefault("issueCount")))
			.ThenByDescending(x => Number(x.GetValueOrDefault("reservationCount")))
			.Take(top)
			.ToList();
	}
}
