using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Receiving;

namespace ShikshaMitra.Testing;

// SQLite cannot order DateTimeOffset. Preserve every UTC tick in this test-only
// mapping; production PostgreSQL keeps its native timestamptz model and guards.
internal sealed class LibraryReceivingSqliteModelCustomizer(ModelCustomizerDependencies dependencies)
	: RelationalModelCustomizer(dependencies)
{
	public override void Customize(ModelBuilder modelBuilder, DbContext context)
	{
		base.Customize(modelBuilder, context);
		if (context.Database.ProviderName != "Microsoft.EntityFrameworkCore.Sqlite") return;
		modelBuilder.Entity<LibraryStockReceipt>().Property(receipt => receipt.RecordedAt)
			.HasConversion(value => value.UtcTicks, ticks => new DateTimeOffset(ticks, TimeSpan.Zero))
			.HasColumnType("INTEGER");
		// SQLite NUMERIC affinity otherwise coerces the decimal(18,2) boundary
		// to 10000000000000000. Native PostgreSQL tests verify actual precision.
		if (modelBuilder.Model.FindEntityType(typeof(Book)) != null)
			modelBuilder.Entity<Book>().Property(book => book.Price)
				.HasConversion<string>().HasColumnType("TEXT");
		// PostgreSQL's array default is only needed for its additive register migration.
		if (modelBuilder.Model.FindEntityType(typeof(PayrollPreviewEmployee)) != null)
			modelBuilder.Entity<PayrollPreviewEmployee>().Property(row => row.RegisterIssueCodes).HasDefaultValueSql(null);
		if (modelBuilder.Model.FindEntityType(typeof(PayrollRunItemSnapshot)) != null)
			modelBuilder.Entity<PayrollRunItemSnapshot>().Property(row => row.RegisterIssueCodes).HasDefaultValueSql(null);
	}
}
