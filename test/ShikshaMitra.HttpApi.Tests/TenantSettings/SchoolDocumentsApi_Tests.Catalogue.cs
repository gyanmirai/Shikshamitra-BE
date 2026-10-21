using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.TenantSettings.SchoolDocuments;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public partial class SchoolDocumentsApi_Tests
{
	[Fact]
	public async Task Catalogue_defines_tenant_only_actions_and_independent_navigation()
	{
		using var scope = factory.Services.CreateScope();
		var manager = scope.ServiceProvider.GetRequiredService<IPermissionDefinitionManager>();
		foreach (var name in new[] { SchoolDocumentPermissions.View, SchoolDocumentPermissions.Add,
			SchoolDocumentPermissions.Delete, SchoolDocumentPermissions.Navigation })
		{
			var permission = await manager.GetAsync(name);
			permission.IsEnabled.ShouldBeTrue();
			permission.MultiTenancySide.ShouldBe(MultiTenancySides.Tenant);
		}
	}
}
