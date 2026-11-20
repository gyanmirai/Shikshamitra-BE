using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Library.Reservations;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	public DbSet<LibraryHold> LibraryHolds { get; set; } = null!;
	public DbSet<LibraryReservationEvent> LibraryReservationEvents { get; set; } = null!;
}
