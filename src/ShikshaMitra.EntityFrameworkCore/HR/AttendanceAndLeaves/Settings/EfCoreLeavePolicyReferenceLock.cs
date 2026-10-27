using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class EfCoreLeavePolicyReferenceLock(IDbContextProvider<ShikshaMitraDbContext> contexts,
	IPayrollInputRevisionStore payrollFence, ICurrentTenant currentTenant)
	: ILeavePolicyReferenceLock, ITransientDependency
{
	public async Task AcquireAsync(Guid policyId, Guid? tenantId)
	{
		if (tenantId != currentTenant.Id)
			throw new InvalidOperationException("Leave policy reference locking requires the current tenant.");
		// SaveChanges and onboarding already take this tenant fence. Always acquire it
		// before a policy row so archive/default/assignment writes cannot invert order.
		await payrollFence.LockAsync();
		var db = await contexts.GetDbContextAsync();
		if (db.Database.IsNpgsql())
		{
			if (db.Database.CurrentTransaction == null)
				throw new InvalidOperationException("Leave policy reference writes require a transaction.");
			await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"AppEmployeeLeavePolicies\" WHERE \"Id\" = {policyId} AND \"TenantId\" IS NOT DISTINCT FROM {tenantId} AND NOT \"IsDeleted\" FOR UPDATE");
		}
		else if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
		{
			await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"AppEmployeeLeavePolicies\" SET \"ConcurrencyStamp\" = \"ConcurrencyStamp\" WHERE \"Id\" = {policyId} AND \"TenantId\" IS {tenantId} AND NOT \"IsDeleted\"");
		}
		else throw new NotSupportedException("Leave policy reference writes require transactional PostgreSQL locking.");
	}
}