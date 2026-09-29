using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
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
				GetRequiredService<IServiceProvider>(), new TestHostEnvironment());
			string[] names =
			[
				ShikshaMitraModulePermissions.Academics,
				ShikshaMitraPermissions.Group.Add,
				ShikshaMitraPermissions.Group.Create,
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

	private sealed class TestHostEnvironment : IHostEnvironment
	{
		public string EnvironmentName { get; set; } = Environments.Production;
		public string ApplicationName { get; set; } = nameof(PermissionBatchRead_Tests);
		public string ContentRootPath { get; set; } = string.Empty;
		public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
	}
}
