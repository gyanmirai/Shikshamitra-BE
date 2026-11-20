using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Catalogue;
using ShikshaMitra.Library.Shared;

namespace ShikshaMitra.Library.Reports;

public partial class LibraryReportGenerator
{
	private async Task<LibraryReportContext> LoadContextAsync(
		LibraryReportFilterDto filters,
		LibraryReportExecutionScope executionScope,
		string reportType)
	{
		var allowedYearIds = executionScope.AllowedAcademicYearIds;
		var memberIds = (await libraryAccess.GetMemberIdsAsync(executionScope.UserScope)).ToArray();
		var bookQuery = (await bookRepository.GetQueryableAsync()).Where(x => x.TenantId == tenant.Id);

		if (filters.BookIds is { Count: > 0 })
		{
			bookQuery = bookQuery.Where(x => filters.BookIds.Contains(x.Id));
		}

		if (filters.CategoryIds is { Count: > 0 })
		{
			bookQuery = bookQuery.Where(x => filters.CategoryIds.Contains(x.CategoryId));
		}

		var books = await asyncExecuter.ToListAsync(bookQuery);
		var bookIds = books.Select(x => x.Id).ToHashSet();

		var allCopies = await asyncExecuter.ToListAsync((await copyRepository.GetQueryableAsync()).Where(x => x.TenantId == tenant.Id));
		var copies = allCopies.Where(x => bookIds.Contains(x.BookId)).ToList();
		var copyIds = copies.Select(x => x.Id).ToHashSet();
		var legacyCopyBooks = allCopies.GroupBy(x => x.AccessionNumber.Trim().ToUpperInvariant(), StringComparer.Ordinal)
			.Where(group => group.Count() == 1 && bookIds.Contains(group.Single().BookId))
			.ToDictionary(group => group.Key, group => group.Single().BookId, StringComparer.Ordinal);
		var accessionNumbers = legacyCopyBooks.Keys.ToArray();

		var issueQuery = await loanScope.GetReadableLoansAsync(executionScope.UserScope);
		var requestQuery = await requestScope.GetReadableRequestsAsync(executionScope.UserScope);
		if (filters.AcademicYearId.HasValue)
		{
			issueQuery = issueQuery.Where(x => x.AcademicYearId == filters.AcademicYearId.Value);
			requestQuery = requestQuery.Where(x => x.AcademicYearId == filters.AcademicYearId.Value);
		}
		if (filters.BookIds is { Count: > 0 } || filters.CategoryIds is { Count: > 0 })
		{
			issueQuery = issueQuery.Where(x => copyIds.Contains(x.BookCopyId) || x.BookCopyId == Guid.Empty &&
				Enumerable.Contains(accessionNumbers, x.AccessionNumber.Trim().ToUpper()));
			requestQuery = requestQuery.Where(x => bookIds.Contains(x.BookId));
		}
		var issues = await asyncExecuter.ToListAsync(issueQuery);
		var reservations = await asyncExecuter.ToListAsync(requestQuery);

		var categories = await asyncExecuter.ToListAsync((await categoryRepository.GetQueryableAsync())
			.Where(x => books.Select(b => b.CategoryId).Contains(x.Id)));
		var chargeCategoriesQuery = (await chargeCategoryRepository.GetQueryableAsync())
			.Where(x => allowedYearIds.Contains(x.AcademicYearId));
		if (filters.AcademicYearId.HasValue)
		{
			chargeCategoriesQuery = chargeCategoriesQuery.Where(x => x.AcademicYearId == filters.AcademicYearId.Value);
		}
		var chargeCategories = await asyncExecuter.ToListAsync(chargeCategoriesQuery);
		var chargeCategoryIds = chargeCategories.Select(x => x.Id).ToHashSet();
		var issueIds = issues.Select(x => x.Id).ToHashSet();

		var charges = await asyncExecuter.ToListAsync((await chargeRepository.GetQueryableAsync())
			.Where(x => Enumerable.Contains(memberIds, x.MemberId) &&
			            (chargeCategoryIds.Contains(x.ChargeCategoryId) ||
			             (x.BookIssueId.HasValue && issueIds.Contains(x.BookIssueId.Value)))));

		var sections = await asyncExecuter.ToListAsync((await sectionRepository.GetQueryableAsync())
			.Where(x => copies.Select(c => c.SectionId).Contains(x.Id)));
		var shelves = await asyncExecuter.ToListAsync((await shelfRepository.GetQueryableAsync())
			.Where(x => copies.Select(c => c.ShelfId).Contains(x.Id)));

		var (members, aliases) = await LoadCurrentMembersAsync(executionScope, issues, reservations, charges, memberIds,
			reportType == LibraryReportTypes.MemberList);

		return new LibraryReportContext(
			books,
			copies,
			issues,
			reservations,
			charges,
			categories.ToDictionary(x => x.Id, x => x.Name),
			chargeCategories.ToDictionary(x => x.Id, x => x.ChargeType),
			sections.ToDictionary(x => x.Id, x => x.SectionName ?? string.Empty),
			shelves.ToDictionary(x => x.Id, x => x.ShelfName ?? string.Empty),
			members, legacyCopyBooks, aliases);
	}
}

internal sealed record LibraryReportContext(
	List<Book> Books,
	List<BookCopy> Copies,
	List<BookIssue> Issues,
	List<BookReservation> Reservations,
	List<LibraryCharge> Charges,
	Dictionary<Guid, string> Categories,
	Dictionary<Guid, string> ChargeCategories,
	Dictionary<Guid, string> Sections,
	Dictionary<Guid, string> Shelves,
	Dictionary<Guid, LibraryMemberReportInfo> Members,
	Dictionary<string, Guid> LegacyCopyBooks,
	Dictionary<Guid, Guid> MemberAliases)
{
	public LibraryReadSchoolDay ReadDay { get; init; } = new(null, "Unavailable", null);
	public LibraryCatalogueLabels CatalogueLabels { get; init; } = new(LibraryCatalogueRules.Defaults());
	public Guid PersonId(BookIssue issue) => issue.MemberRole == 0 ? MemberAliases.GetValueOrDefault(issue.MemberId, issue.MemberId) : issue.MemberId;
	public Guid PersonId(BookReservation request) => request.CanonicalMemberId ?? MemberAliases.GetValueOrDefault(request.MemberId, request.MemberId);
	public Dictionary<Guid, Book> BookById { get; } = Books.ToDictionary(x => x.Id);
	public Dictionary<Guid, BookIssue> IssueById { get; } = Issues.ToDictionary(x => x.Id);
	private Dictionary<Guid, Guid> BookByCopyId { get; } = Copies.ToDictionary(x => x.Id, x => x.BookId);
	public Book? ResolveBook(BookIssue issue)
	{
		if (issue.BookCopyId != Guid.Empty)
			return BookByCopyId.TryGetValue(issue.BookCopyId, out var bookId) ? BookById.GetValueOrDefault(bookId) : null;
		return LegacyCopyBooks.TryGetValue(issue.AccessionNumber.Trim().ToUpperInvariant(), out var legacyBookId)
			? BookById.GetValueOrDefault(legacyBookId) : null;
	}
}

internal sealed record LibraryMemberReportInfo(
	Guid Id,
	int RoleValue,
	string Role,
	string Name,
	string? Identifier,
	string? Email,
	string? Phone,
	string? ClassDivision,
	DateTime JoinDate,
	bool IsCurrentMember);
