using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Seeders;
using Volo.Abp.DependencyInjection;
using Volo.Abp.PermissionManagement;

namespace ShikshaMitra.EntityFrameworkCore.Seeders;

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IPermissionDataSeeder))]
public class IdempotentPermissionDataSeeder(IPermissionGrantSeedRepository permissionGrantSeedRepository)
	: IPermissionDataSeeder, ITransientDependency
{
	public async Task SeedAsync(
		string providerName,
		string providerKey,
		IEnumerable<string> grantedPermissions,
		Guid? tenantId = null)
	{
		var permissionNames = grantedPermissions
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();

		await permissionGrantSeedRepository.InsertGrantsIgnoringDuplicatesAsync(
			providerName,
			providerKey,
			permissionNames,
			tenantId);
	}
}
