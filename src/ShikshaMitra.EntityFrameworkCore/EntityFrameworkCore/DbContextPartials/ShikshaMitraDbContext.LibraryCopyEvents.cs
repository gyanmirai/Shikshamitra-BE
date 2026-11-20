using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Library.Books;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	public DbSet<LibraryCopyEvent> LibraryCopyEvents { get; set; } = null!;
}
