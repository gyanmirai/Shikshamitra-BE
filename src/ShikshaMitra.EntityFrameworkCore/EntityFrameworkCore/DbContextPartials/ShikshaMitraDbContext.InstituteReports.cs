using Microsoft.EntityFrameworkCore;
using ShikshaMitra.TenantSettings.Reports;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	public DbSet<InstituteReportJob> InstituteReportJobs { get; set; }
}
