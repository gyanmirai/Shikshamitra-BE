using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public class AcademicPermissionUserScopeSeederContributor(
	ICurrentTenant currentTenant,
	IGuidGenerator guidGenerator,
	IUnitOfWork unitOfWork,
	AcademicPermissionUserScopeSyncer syncer,
	ILogger<AcademicPermissionUserScopeSeederContributor> logger)
	: BaseSeederContributor(currentTenant, guidGenerator, unitOfWork)
{
	public override async Task SeedAsync(DataSeedContext context)
	{
		var inserted = await syncer.SyncAsync();
		await CurrentUnitOfWork.SaveChangesAsync();

		logger.LogInformation(
			"[AcademicPermissionUserScopeSeeder] Synced user scopes from Academic Permissions. Inserted={InsertedCount}",
			inserted);
	}
}
