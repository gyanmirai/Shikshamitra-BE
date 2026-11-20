using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Library.Custody;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	public DbSet<LibraryCustodyHandoff> LibraryCustodyHandoffs { get; set; } = null!;
	public DbSet<LibraryCustodyItem> LibraryCustodyItems { get; set; } = null!;
	public DbSet<LibraryCustodyReceipt> LibraryCustodyReceipts { get; set; } = null!;
}
