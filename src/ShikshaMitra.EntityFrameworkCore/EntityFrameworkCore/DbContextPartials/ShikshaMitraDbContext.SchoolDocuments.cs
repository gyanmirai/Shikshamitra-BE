using Microsoft.EntityFrameworkCore;
using ShikshaMitra.TenantSettings.SchoolDocuments;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	public DbSet<SchoolDocument> SchoolDocuments { get; set; }
	public DbSet<SchoolDocumentFile> SchoolDocumentFiles { get; set; }
}
