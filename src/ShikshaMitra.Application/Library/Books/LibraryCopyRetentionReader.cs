using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Receiving;
using ShikshaMitra.Library.Shared;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Books;

internal sealed record LibraryCopyRetention(Guid? ReceiptId, string? BlockedReason);

public sealed class LibraryCopyRetentionReader(IRepository<BookIssue, Guid> loans,
	IRepository<LibraryCopyEvent, Guid> events, IRepository<LibraryReceivedCopy, Guid> received,
	IRepository<LibraryStockReceiptLine, Guid> receiptLines, IAsyncQueryableExecuter queries,
	ICurrentTenant tenant) : ITransientDependency
{
	internal async Task<Dictionary<Guid, LibraryCopyRetention>> ReadAsync(IReadOnlyCollection<BookCopy> copies)
	{
		if (copies.Count == 0) return [];
		var ids = copies.Select(copy => copy.Id).ToArray();
		var accessions = copies.Select(copy => copy.AccessionNumber.ToUpperInvariant()).ToArray();
		var loanRows = await queries.ToListAsync((await loans.GetQueryableAsync()).Where(loan => loan.TenantId == tenant.Id &&
			(Enumerable.Contains(ids, loan.BookCopyId) || Enumerable.Contains(accessions, loan.AccessionNumber.ToUpper())))
			.Select(loan => new { loan.BookCopyId, Accession = loan.AccessionNumber.ToUpper() }));
		var loanIds = loanRows.Select(loan => loan.BookCopyId).ToHashSet();
		var loanAccessions = loanRows.Select(loan => loan.Accession).ToHashSet(StringComparer.Ordinal);
		var eventIds = (await queries.ToListAsync((await events.GetQueryableAsync())
			.Where(row => row.TenantId == tenant.Id && Enumerable.Contains(ids, row.CopyId)).Select(row => row.CopyId))).ToHashSet();
		var lineQuery = (await receiptLines.GetQueryableAsync()).Where(line => line.TenantId == tenant.Id);
		var receivingQuery = (await received.GetQueryableAsync()).Where(link => link.TenantId == tenant.Id && Enumerable.Contains(ids, link.CopyId));
		var receivingRows = await queries.ToListAsync(from link in receivingQuery
			join line in lineQuery on link.ReceiptLineId equals line.Id into matchingLines
			from line in matchingLines.DefaultIfEmpty()
			select new { link.CopyId, ReceiptId = line == null ? (Guid?)null : line.ReceiptId });
		var receivingByCopy = receivingRows.ToDictionary(link => link.CopyId, link => link.ReceiptId);
		return copies.ToDictionary(copy => copy.Id, copy =>
		{
			var hasReceiving = receivingByCopy.TryGetValue(copy.Id, out var receiptId);
			var reason = hasReceiving ? "This copy has receiving provenance. Retain the copy and use its lifecycle actions."
				: loanIds.Contains(copy.Id) || loanAccessions.Contains(copy.AccessionNumber.ToUpperInvariant())
					? "This copy has circulation history that must be retained."
					: copy.Status != BookCopyStatus.Available ? "This copy has a recorded condition or circulation state. Use its lifecycle actions."
					: eventIds.Contains(copy.Id) ? "This copy has recorded history that must be retained." : null;
			return new LibraryCopyRetention(receiptId, reason);
		});
	}
}
