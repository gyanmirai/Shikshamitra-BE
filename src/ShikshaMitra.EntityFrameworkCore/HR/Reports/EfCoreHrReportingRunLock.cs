using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.HR.Reports;

public class EfCoreHrReportingRunLock(IDbContextProvider<ShikshaMitraDbContext> contexts) : IHrReportingRunLock, ITransientDependency
{
	public async Task LockAsync(Guid runId, Guid? tenantId)
	{
		var db = await contexts.GetDbContextAsync();
		if (db.Database.IsNpgsql())
			await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"HR_ReportRuns\" WHERE \"Id\" = {runId} AND \"TenantId\" = {tenantId} AND NOT \"IsDeleted\" FOR UPDATE");
		else if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
			await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"HR_ReportRuns\" SET \"ConcurrencyStamp\" = \"ConcurrencyStamp\" WHERE \"Id\" = {runId} AND \"TenantId\" = {tenantId} AND NOT \"IsDeleted\"");
		else throw new NotSupportedException("HR report exports require transactional PostgreSQL row locking.");
	}
}
