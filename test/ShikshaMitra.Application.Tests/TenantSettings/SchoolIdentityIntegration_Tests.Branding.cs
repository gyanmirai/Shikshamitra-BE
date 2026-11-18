using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public partial class SchoolIdentityIntegration_Tests
{
	[Fact]
	public Task Shared_branding_reads_persisted_school_identity_and_legacy_location_text() => RunAsync(async (_, _) =>
	{
		var tenants = GetRequiredService<ITenantRepository>();
		var tenant = await tenants.InsertAsync(await GetRequiredService<TenantManager>().CreateAsync($"branding-{Guid.NewGuid():N}"), true);
		using var school = GetRequiredService<ICurrentTenant>().Change(tenant.Id);
		tenant.SetProperty("InstituteName", "  Sample School  ");
		tenant.SetProperty("Address1", "School Road");
		tenant.SetProperty("Address2", "");
		tenant.SetProperty("City", new { name = "Surat" });
		tenant.SetProperty("State", new { Name = "Gujarat" });
		tenant.SetProperty("PostalCode", "395001");
		tenant.SetProperty("Country", new { label = "India" });
		await tenants.UpdateAsync(tenant, true);

		var result = await Service.GetBrandingAsync();
		result.SchoolName.ShouldBe("Sample School");
		result.AddressLines.ShouldBe(new[] { "School Road", "Surat, Gujarat 395001", "India" });
		result.Logo.ShouldBeNull();
		result.Banner.ShouldBeNull();
	});

	[Fact]
	public Task Shared_branding_does_not_invent_missing_school_address() => RunAsync(async (_, _) =>
	{
		var tenants = GetRequiredService<ITenantRepository>();
		var tenant = await tenants.InsertAsync(await GetRequiredService<TenantManager>().CreateAsync($"branding-{Guid.NewGuid():N}"), true);
		using var school = GetRequiredService<ICurrentTenant>().Change(tenant.Id);
		tenant.ExtraProperties.Clear();
		await tenants.UpdateAsync(tenant, true);

		var result = await Service.GetBrandingAsync();
		result.SchoolName.ShouldBe(tenant.Name);
		result.AddressLines.ShouldBeEmpty();
	});
}
