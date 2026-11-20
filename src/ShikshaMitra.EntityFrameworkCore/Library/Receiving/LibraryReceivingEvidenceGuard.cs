using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Library.Books;
using Volo.Abp;

namespace ShikshaMitra.Library.Receiving;

public static class LibraryReceivingEvidenceGuard
{
	public static void Validate(DbContext context)
	{
		var headers = context.ChangeTracker.Entries<LibraryStockReceipt>().ToArray();
		var lines = context.ChangeTracker.Entries<LibraryStockReceiptLine>().ToArray();
		var links = context.ChangeTracker.Entries<LibraryReceivedCopy>().ToArray();
		var commands = context.ChangeTracker.Entries<LibraryStockReceiptCommand>().ToArray();
		if (headers.Any(row => row.State is EntityState.Modified or EntityState.Deleted) ||
			lines.Any(row => row.State is EntityState.Modified or EntityState.Deleted) ||
			links.Any(row => row.State is EntityState.Modified or EntityState.Deleted) ||
			commands.Any(row => row.State is EntityState.Modified or EntityState.Deleted))
			throw new InvalidOperationException("Committed stock receiving evidence cannot be edited or deleted.");

		var addedHeaders = headers.Where(row => row.State == EntityState.Added).ToDictionary(row => row.Entity.Id, row => row.Entity);
		var addedLines = lines.Where(row => row.State == EntityState.Added).ToDictionary(row => row.Entity.Id, row => row.Entity);
		var addedLinks = links.Where(row => row.State == EntityState.Added).Select(row => row.Entity).ToArray();
		var addedCommands = commands.Where(row => row.State == EntityState.Added).Select(row => row.Entity).ToArray();
		if (addedHeaders.Count == 0 && addedLines.Count == 0 && addedLinks.Length == 0 && addedCommands.Length == 0) return;
		var addedCopies = context.ChangeTracker.Entries<BookCopy>().Where(row => row.State == EntityState.Added)
			.ToDictionary(row => row.Entity.Id, row => row.Entity);
		foreach (var line in addedLines.Values)
			if (!addedHeaders.TryGetValue(line.ReceiptId, out var header) || header.TenantId != line.TenantId ||
				line.LineNumber < 1 || line.LineNumber > header.ExpectedLineCount) throw Invalid();
		foreach (var link in addedLinks)
			if (!addedLines.TryGetValue(link.ReceiptLineId, out var line) || line.TenantId != link.TenantId ||
				!addedCopies.TryGetValue(link.CopyId, out var copy) || copy.TenantId != link.TenantId ||
				copy.BookId != line.BookId || copy.SectionId != line.SectionId || copy.ShelfId != line.ShelfId ||
				copy.AccessionNumber != link.AccessionNumber || copy.CopyNumber != link.CopyNumber || copy.Status != BookCopyStatus.Available)
				throw Invalid();
		foreach (var command in addedCommands)
			if (!addedHeaders.TryGetValue(command.ReceiptId, out var header) || header.TenantId != command.TenantId ||
				header.RecordedBy != command.ActorId || header.RecordedAt != command.RecordedAt) throw Invalid();

		var lineCounts = addedLines.Values.GroupBy(row => row.ReceiptId).ToDictionary(group => group.Key, group => group.Count());
		var copyCounts = addedLinks.GroupBy(row => row.ReceiptLineId).ToDictionary(group => group.Key, group => group.Count());
		var expectedCopies = addedLines.Values.GroupBy(row => row.ReceiptId).ToDictionary(group => group.Key, group => group.Sum(row => row.ExpectedCopyCount));
		var commandCounts = addedCommands.GroupBy(row => row.ReceiptId).ToDictionary(group => group.Key, group => group.Count());
		foreach (var header in addedHeaders.Values)
			if (!header.TenantId.HasValue || header.ExpectedLineCount != lineCounts.GetValueOrDefault(header.Id) ||
				header.ExpectedCopyCount != expectedCopies.GetValueOrDefault(header.Id) || commandCounts.GetValueOrDefault(header.Id) != 1)
				throw Invalid();
		foreach (var line in addedLines.Values)
			if (line.ExpectedCopyCount != copyCounts.GetValueOrDefault(line.Id)) throw Invalid();
	}

	private static BusinessException Invalid() => new(LibraryErrorCodes.ReceivingInvalid,
		"Save the complete new receiving record, its new physical copies and the actual accepted command together.");
}
