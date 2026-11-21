using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;

namespace ShikshaMitra.Library.Members;

public partial class LibraryMemberAppService
{
	private async Task<IQueryable<BookIssue>> GetMemberLoanQueryAsync(Guid memberId)
	{
		var ids = await libraryAccess.GetPersonMemberIdsAsync(memberId);
		var context = await libraryAccess.GetContextAsync();
		return (await loanScope.GetReadableLoansAsync(context.UserScope)).Where(row => ids.Contains(row.MemberId));
	}

	private async Task<IQueryable<BookReservation>> GetMemberRequestQueryAsync(Guid memberId)
	{
		var ids = await libraryAccess.GetPersonMemberIdsAsync(memberId);
		var context = await libraryAccess.GetContextAsync();
		return (await requestScope.GetReadableRequestsAsync(context.UserScope)).Where(row =>
			row.CanonicalMemberId.HasValue ? ids.Contains(row.CanonicalMemberId.Value) : ids.Contains(row.MemberId));
	}

	private async Task<Dictionary<Guid, MemberIssueBook>> GetMemberIssueBooksAsync(IReadOnlyCollection<BookIssue> issues)
	{
		if (issues.Count == 0) return [];
		var copyIds = issues.Where(row => row.BookCopyId != Guid.Empty).Select(row => row.BookCopyId).Distinct().ToArray();
		var legacyAccessions = issues.Where(row => row.BookCopyId == Guid.Empty).Select(row => row.AccessionNumber.Trim().ToUpperInvariant()).Distinct().ToArray();
		var copies = await AsyncExecuter.ToListAsync((await bookCopyRepository.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id &&
			(Enumerable.Contains(copyIds, row.Id) || Enumerable.Contains(legacyAccessions, row.NormalizedAccessionNumber)))
			.Select(row => new MemberCopy(row.Id, row.BookId, row.NormalizedAccessionNumber)));
		var bookIds = copies.Select(row => row.BookId).Distinct().ToArray();
		var books = await AsyncExecuter.ToListAsync((await bookRepository.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id &&
			Enumerable.Contains(bookIds, row.Id)).Select(row => new { row.Id, row.Title, row.CategoryId }));
		var categoryIds = books.Select(row => row.CategoryId).Distinct().ToArray();
		var categories = (await AsyncExecuter.ToListAsync((await bookCategoryRepository.GetQueryableAsync()).Where(row =>
			row.TenantId == CurrentTenant.Id && Enumerable.Contains(categoryIds, row.Id)).Select(row => new { row.Id, row.Name })))
			.ToDictionary(row => row.Id, row => row.Name);
		var byCopy = copies.ToDictionary(row => row.Id);
		var byAccession = copies.GroupBy(row => row.Accession, StringComparer.Ordinal).ToDictionary(group => group.Key,
			group => group.Count() == 1 ? group.Single() : null, StringComparer.Ordinal);
		var byBook = books.ToDictionary(row => row.Id);
		return issues.ToDictionary(row => row.Id, row =>
		{
			var stable = row.BookCopyId != Guid.Empty;
			var copy = stable ? byCopy.GetValueOrDefault(row.BookCopyId) : byAccession.GetValueOrDefault(row.AccessionNumber.Trim().ToUpperInvariant());
			var book = copy == null ? null : byBook.GetValueOrDefault(copy.BookId);
			return new MemberIssueBook(stable ? row.BookCopyId : null, book?.Id, book?.Title,
				book == null ? null : categories.GetValueOrDefault(book.CategoryId), book == null ? "Unavailable" :
				stable ? "CopyId" : "LegacyAccessionCandidate", !stable || copy == null || book == null ||
				!string.Equals(copy.Accession, row.AccessionNumber.Trim().ToUpperInvariant(), StringComparison.Ordinal));
		});
	}

	private sealed record MemberCopy(Guid Id, Guid BookId, string Accession);
	private sealed record MemberIssueBook(Guid? CopyId, Guid? BookId, string? Title, string? Category, string LinkSource, bool RequiresReconciliation);
}
