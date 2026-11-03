using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Xunit;

namespace ShikshaMitra.Security.Permissions;

public class PermissionBatchRead_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Batch_effective_grants_match_individual_checks()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var checker = ActivatorUtilities.CreateInstance<ShikshaMitraPermissionChecker>(
				GetRequiredService<IServiceProvider>());
			string[] names =
			[
				ShikshaMitraModulePermissions.Academics,
				ShikshaMitraPermissions.Group.Add,
				ShikshaMitraPermissions.Group.Default + ".Create", // Retained legacy permission identifier.
				"AbpIdentity.Users",
				"AbpIdentity.Users.View",
				"Unknown.Future.Permission"
			];
			IPermissionChecker interfaceChecker = checker;
			var batch = await interfaceChecker.IsGrantedAsync(names);
			foreach (var name in names)
				(batch.Result[name] == PermissionGrantResult.Granted)
					.ShouldBe(await checker.IsGrantedAsync(name), name);
		});
	}

}
