using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Library.CampusTransfers;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	public DbSet<LibraryCampusTransfer> LibraryCampusTransfers { get; set; } = null!;
	public DbSet<LibraryCampusTransferItem> LibraryCampusTransferItems { get; set; } = null!;
	public DbSet<LibraryCampusTransferReceipt> LibraryCampusTransferReceipts { get; set; } = null!;
}
