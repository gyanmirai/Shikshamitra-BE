using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace ShikshaMitra.Library.InventoryVerification;

internal static class LibraryStockVerificationEvidenceGuard
{
	public static void Validate(DbContext context)
	{
		if (context.ChangeTracker.Entries<LibraryStockVerificationBaseline>().Any(entry => entry.State is EntityState.Modified or EntityState.Deleted) ||
			context.ChangeTracker.Entries<LibraryStockVerificationObservation>().Any(entry => entry.State is EntityState.Modified or EntityState.Deleted) ||
			context.ChangeTracker.Entries<LibraryStockVerificationCommand>().Any(entry => entry.State is EntityState.Modified or EntityState.Deleted) ||
			context.ChangeTracker.Entries<LibraryStockVerificationDecision>().Any(entry => entry.State is EntityState.Modified or EntityState.Deleted) ||
			context.ChangeTracker.Entries<LibraryStockVerificationResult>().Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
			throw new InvalidOperationException("Stock verification evidence is append-only.");
		if (context.ChangeTracker.Entries<LibraryStockVerificationSession>().Any(entry => entry.State == EntityState.Deleted ||
			entry.State == EntityState.Modified && (entry.Property(row => row.IsDeleted).IsModified ||
				entry.Property(row => row.Status).OriginalValue is LibraryStockVerificationStatus.Finalized or LibraryStockVerificationStatus.Cancelled)))
			throw new InvalidOperationException("Stock verification sessions are retained, and closed sessions are sealed.");
	}
}
