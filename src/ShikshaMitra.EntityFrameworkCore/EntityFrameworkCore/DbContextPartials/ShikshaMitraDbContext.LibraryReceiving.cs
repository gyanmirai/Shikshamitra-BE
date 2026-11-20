using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Library.Receiving;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	public DbSet<LibraryStockReceipt> LibraryStockReceipts { get; set; } = null!;
	public DbSet<LibraryStockReceiptLine> LibraryStockReceiptLines { get; set; } = null!;
	public DbSet<LibraryReceivedCopy> LibraryReceivedCopies { get; set; } = null!;
	public DbSet<LibraryStockReceiptCommand> LibraryStockReceiptCommands { get; set; } = null!;
	public DbSet<LibraryStockImportBatch> LibraryStockImportBatches { get; set; } = null!;
	public DbSet<LibraryStockImportRow> LibraryStockImportRows { get; set; } = null!;
}
