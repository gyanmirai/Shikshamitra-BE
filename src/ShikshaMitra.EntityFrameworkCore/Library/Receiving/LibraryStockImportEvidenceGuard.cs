using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;

namespace ShikshaMitra.Library.Receiving;

public static class LibraryStockImportEvidenceGuard
{
	public static void Validate(DbContext context)
	{
		var batches = context.ChangeTracker.Entries<LibraryStockImportBatch>().ToArray();
		var rows = context.ChangeTracker.Entries<LibraryStockImportRow>().ToArray();
		if (batches.Any(row => row.State is EntityState.Modified or EntityState.Deleted) ||
			rows.Any(row => row.State is EntityState.Modified or EntityState.Deleted))
			throw new InvalidOperationException("Committed stock import outcomes cannot be edited or deleted.");

		var addedBatches = batches.Where(entry => entry.State == EntityState.Added).Select(entry => entry.Entity).ToArray();
		var addedRows = rows.Where(entry => entry.State == EntityState.Added).Select(entry => entry.Entity)
			.ToLookup(row => (row.TenantId, row.BatchId));
		if (addedRows.Count != addedBatches.Length) throw Invalid();
		foreach (var batch in addedBatches)
		{
			var outcomes = addedRows[(batch.TenantId, batch.Id)];
			if (!batch.TenantId.HasValue || batch.RowCount is < 1 or > LibraryStockImportBatch.MaximumRows ||
				batch.RowCount != outcomes.Count() || batch.NewTitleCount != outcomes.Where(row => row.IsNewTitle)
					.Select(row => row.BookId).Distinct().Count()) throw Invalid();
		}
	}

	private static BusinessException Invalid() => new(LibraryErrorCodes.StockImportInvalid,
		"Save the accepted import batch together with every committed copy outcome.");
}
