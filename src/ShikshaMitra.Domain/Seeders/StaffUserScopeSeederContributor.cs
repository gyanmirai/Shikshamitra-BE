using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

/// <summary>
/// Retained for compatibility with old seeder references.
/// User scope is now derived from Academic Permissions by AcademicPermissionUserScopeSeederContributor.
/// </summary>
public class StaffUserScopeSeederContributor(
	ICurrentTenant currentTenant,
	IGuidGenerator guidGenerator,
	IUnitOfWork unitOfWork,
	ILogger<StaffUserScopeSeederContributor> logger)
	: BaseSeederContributor(currentTenant, guidGenerator, unitOfWork)
{
	public override Task SeedAsync(DataSeedContext context)
	{
		logger.LogInformation(
			"[StaffUserScopeSeeder] Skipped. User scope is derived from Academic Permissions.");
		return Task.CompletedTask;
	}
}
