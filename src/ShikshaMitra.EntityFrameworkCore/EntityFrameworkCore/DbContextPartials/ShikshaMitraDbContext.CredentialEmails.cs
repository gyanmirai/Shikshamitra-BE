using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.Portal;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	public DbSet<StudentCredentialEmailRequest> StudentCredentialEmailRequests { get; set; }
}
