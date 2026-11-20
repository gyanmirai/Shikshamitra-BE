using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Library.Catalogue;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	public DbSet<LibraryCatalogueConfiguration> LibraryCatalogueConfigurations { get; set; } = null!;
}
