using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Library.Shared;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	public DbSet<LibraryBorrowerScopeCapture> LibraryBorrowerScopeCaptures { get; set; } = null!;
	public DbSet<LibraryBorrowerAssignmentSnapshot> LibraryBorrowerAssignmentSnapshots { get; set; } = null!;
}
