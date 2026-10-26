using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class EfCoreLeavePolicyReferenceLock(IDbContextProvider<ShikshaMitraDbContext> contexts)
	: ILeavePolicyReferenceLock, ITransientDependency
{
	public async Task AcquireAsync(Guid policyId, Guid? tenantId)
	{
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