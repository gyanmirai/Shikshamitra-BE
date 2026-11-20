using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Library.Books;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	public DbSet<LibraryLoanCommand> LibraryLoanCommands { get; set; } = null!;
	public DbSet<LibraryLoanEvent> LibraryLoanEvents { get; set; } = null!;
}
