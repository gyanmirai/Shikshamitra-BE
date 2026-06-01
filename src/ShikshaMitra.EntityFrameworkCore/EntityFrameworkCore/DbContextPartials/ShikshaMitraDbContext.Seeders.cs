using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Seeders;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
    public DbSet<SeedExecutionMarker> SeedExecutionMarkers { get; set; }
}
