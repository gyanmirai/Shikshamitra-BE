using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class EfCorePayrollInputRevisionStore(IDbContextProvider<ShikshaMitraDbContext> provider, ICurrentTenant tenant)
	: IPayrollInputRevisionStore, ITransientDependency
{
	public async Task<Guid> ReadAsync()
	{
		var db = await provider.GetDbContextAsync();
		var tenantKey = tenant.Id ?? Guid.Empty;
		return await db.Set<PayrollInputRevision>().AsNoTracking().Where(x => x.Id == tenantKey)
			.Select(x => x.Revision).FirstOrDefaultAsync();
	}
	public async Task<Guid> LockAsync()
	{
		var db = await provider.GetDbContextAsync();
		if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Payroll freshness fencing requires a transaction.");
		var id = tenant.Id ?? Guid.Empty;
		await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"HR_PayrollInputRevisions\" (\"Id\", \"TenantId\", \"Revision\") VALUES ({id}, {tenant.Id}, {Guid.Empty}) ON CONFLICT (\"Id\") DO UPDATE SET \"Revision\" = \"HR_PayrollInputRevisions\".\"Revision\"");
		return await ReadAsync();
	}
}
