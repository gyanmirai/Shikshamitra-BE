using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;

namespace ShikshaMitra.Library.IssueReturn;

public partial class BookIssueAppService
{
	private async Task<BookIssue?> FindOpenIssueAsync(BookCopy? copy, string accession)
	{
		var copyId = copy?.Id ?? Guid.Empty;
		var normalized = (copy?.AccessionNumber ?? NormalizeAccession(accession)).ToUpperInvariant();
		var rows = await AsyncExecuter.ToListAsync((await issueRepository.GetQueryableAsync()).Where(loan =>
			loan.Status == BookIssueStatus.Issued && (copyId != Guid.Empty && loan.BookCopyId == copyId ||
				loan.AccessionNumber.Trim().ToUpper() == normalized)).Take(2));
		if (rows.Count > 1)
			throw LibraryException(LibraryErrorCodes.DuplicateActiveIssue,
				"This copy has conflicting open loan records. Review and reconcile them before processing a return.");
		return rows.SingleOrDefault();
	}

	private static void EnsureConfirmedCopy(BookIssue issue, BookCopy? copy)
	{
		if (copy == null || issue.BookCopyId != copy.Id)
			throw LibraryException(LibraryErrorCodes.CopyLoanReconciliationRequired,
				"Review and reconcile this loan's physical copy reference before returning it. An accession match alone does not confirm the copy.");
	}
}
