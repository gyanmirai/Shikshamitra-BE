using System;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

public class RoomFeatureDataSeedContributor(
	IRepository<RoomFeature, Guid> repository, ICurrentTenant currentTenant, IGuidGenerator guidGenerator)
	: IDataSeedContributor, ITransientDependency
{
	[UnitOfWork]
	public virtual async Task SeedAsync(DataSeedContext context)
	{
		using (currentTenant.Change(context.TenantId))
		{
			foreach (var name in RoomFeatureDefaults.Names)
			{
				var normalized = name.ToUpperInvariant();
				if (!await repository.AnyAsync(x => x.NormalizedName == normalized))
					await repository.InsertAsync(new RoomFeature(guidGenerator.Create(), context.TenantId, name));
			}
		}
	}
}
