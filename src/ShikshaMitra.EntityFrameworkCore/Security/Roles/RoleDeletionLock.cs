using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Security.Roles;

public class RoleDeletionLock(IDbContextProvider<ShikshaMitraDbContext> contexts, ICurrentTenant tenant)
	: IRoleDeletionLock, ITransientDependency
{
	public async Task AcquireAsync(Guid roleId)
	{
		var context = await contexts.GetDbContextAsync();
		if (!context.Database.IsNpgsql()) return;
		if (context.Database.CurrentTransaction == null)
			throw new InvalidOperationException("Role deletion requires a transactional unit of work.");

		// Foreign-key inserts take KEY SHARE; hold UPDATE until the usage check and deletion commit.
		await context.Database.ExecuteSqlInterpolatedAsync($"""
			SELECT 1 FROM "AbpRoles"
			WHERE "Id" = {roleId} AND "TenantId" IS NOT DISTINCT FROM {tenant.Id}
			FOR UPDATE
			""");
	}
}
