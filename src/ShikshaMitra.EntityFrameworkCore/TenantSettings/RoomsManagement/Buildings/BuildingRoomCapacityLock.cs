using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Buildings;

public class BuildingRoomCapacityLock(IDbContextProvider<ShikshaMitraDbContext> contexts, ICurrentTenant tenant)
	: IBuildingRoomCapacityLock, ITransientDependency
{
	public async Task AcquireAsync(Guid buildingId)
	{
		var context = await contexts.GetDbContextAsync();
		if (!context.Database.IsNpgsql()) return;
		if (context.Database.CurrentTransaction == null)
			throw new InvalidOperationException("Room capacity validation requires a transactional unit of work.");

		// Room placements and floor-limit changes hold the same parent lock until commit.
		await context.Database.ExecuteSqlInterpolatedAsync($"""
			SELECT 1 FROM "academic_Buildings"
			WHERE "Id" = {buildingId} AND "TenantId" IS NOT DISTINCT FROM {tenant.Id}
			FOR UPDATE
			""");
	}
}
