using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Library.Policies;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	public DbSet<LibraryBorrowingPolicyVersion> LibraryBorrowingPolicyVersions { get; set; } = null!;
	public DbSet<LibraryCalendarClosure> LibraryCalendarClosures { get; set; } = null!;
}
