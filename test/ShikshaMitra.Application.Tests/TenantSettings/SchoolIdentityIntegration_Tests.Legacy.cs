using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.TenantManagement;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public partial class SchoolIdentityIntegration_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Legacy_import_is_idempotent_preserves_original_bytes_and_never_overwrites_configured_defaults(bool base64) => WithUnitOfWorkAsync(async () =>
	{
		var tenants = GetRequiredService<ITenantRepository>();
		var tenant = await GetRequiredService<TenantManager>().CreateAsync($"identity-legacy-{Guid.NewGuid():N}");
		await tenants.InsertAsync(tenant, true);
		using var tenantScope = GetRequiredService<ICurrentTenant>().Change(tenant.Id);
		object originalValue = base64 ? Convert.ToBase64String(Png) : Png;
		tenant.SetProperty("Logo", originalValue); tenant.SetProperty("Banner", originalValue); tenant.SetProperty("PrincipleSign", originalValue);
		await tenants.UpdateAsync(tenant, true);
		var importer = GetRequiredService<SchoolIdentityLegacyMigrationContributor>();
		await importer.SeedAsync(new DataSeedContext(tenant.Id));
		var profiles = GetRequiredService<IRepository<SchoolIdentityProfile, Guid>>();
		var assets = GetRequiredService<IRepository<SchoolIdentityAsset, Guid>>();
		var imported = (await profiles.GetListAsync(x => x.ScopeKey == "school")).Single();
		var importedId = imported.Choices.Logo.AssetId;
		var count = await assets.GetCountAsync();
		count.ShouldBe(3); (await assets.GetListAsync()).ShouldAllBe(asset => asset.IsRetained);
		await importer.SeedAsync(new DataSeedContext(tenant.Id));
		(await assets.GetCountAsync()).ShouldBe(count);
		imported.Replace(imported.Choices with { Logo = new() { Mode = SchoolIdentityAssetMode.Hidden } }, imported.ConcurrencyStamp);
		await profiles.UpdateAsync(imported, true);
		await importer.SeedAsync(new DataSeedContext(tenant.Id));
		(await profiles.GetAsync(imported.Id)).Choices.Logo.Mode.ShouldBe(SchoolIdentityAssetMode.Hidden);
		(await assets.GetAsync(importedId!.Value)).IsRetained.ShouldBeTrue();
		var original = await tenants.GetAsync(tenant.Id);
		foreach (var property in new[] { "Logo", "Banner", "PrincipleSign" })
		{
			if (base64) original.GetProperty(property).ShouldBe(Convert.ToBase64String(Png));
			else original.GetProperty(property).ShouldBeOfType<byte[]>().ShouldBe(Png);
		}
	});
}
