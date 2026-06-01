using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.ObjectExtending;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public class TenantSeederContributor(
	ICurrentTenant currentTenant,
	IGuidGenerator guidGenerator,
	IUnitOfWork unitOfWork,
	ITenantManager tenantManager,
	ITenantRepository tenantRepository) : BaseSeederContributor(currentTenant, guidGenerator, unitOfWork), ITransientDependency
{
	public static readonly Guid DdvsTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

	public override async Task SeedAsync(DataSeedContext context)
	{
		if (context.TenantId.HasValue)
		{
			return;
		}

		// List tenant creation details with hardcoded GUIDs for consistency
		var tenants = new List<(Guid Id, string Name, string AdminEmail, string InstituteName, string Address, string City, string State, string Country, string PostalCode, string ContactNo)>
			{
				(DdvsTenantId, "DDVS", "admin-ddvs@ddvs.com", "DDVS", "Parvat Patiya", "Surat", "Gujarat", "India", "395010", "+91-9876543210")
			};

		var tenantIds = new List<Guid>();

		foreach (var tenantInfo in tenants)
		{
			var existingTenants = await tenantRepository.GetListAsync();
			var existingTenant = existingTenants.FirstOrDefault(tenant =>
				tenant.Id == tenantInfo.Id ||
				string.Equals(tenant.Name, tenantInfo.Name, StringComparison.OrdinalIgnoreCase));
			if (existingTenant != null)
			{
				tenantIds.Add(existingTenant.Id);
				continue;
			}

			// Create tenant input dto.
			TenantCreateDto input = new TenantCreateDto
			{
				Name = tenantInfo.Name,
				AdminEmailAddress = tenantInfo.AdminEmail,
				AdminPassword = "Temp@123"
			};

			input.ExtraProperties.Add("InstituteName", tenantInfo.InstituteName);
			input.ExtraProperties.Add("Address", tenantInfo.Address);
			input.ExtraProperties.Add("City", tenantInfo.City);
			input.ExtraProperties.Add("State", tenantInfo.State);
			input.ExtraProperties.Add("Country", tenantInfo.Country);
			input.ExtraProperties.Add("PostalCode", tenantInfo.PostalCode);
			input.ExtraProperties.Add("ContactNo", tenantInfo.ContactNo);

			// Create tenant using TenantManager.
			var tenant = await tenantManager.CreateAsync(input.Name);
			// Override the generated ID with our hardcoded ID
			typeof(Tenant).GetProperty("Id")?.SetValue(tenant, tenantInfo.Id);

			input.MapExtraPropertiesTo(tenant);

			await tenantRepository.InsertAsync(tenant);
			await unitOfWork.SaveChangesAsync();

			tenantIds.Add(tenant.Id);
		}

		// Store all tenant IDs in the data seed context.
		context.Properties["TenantIds"] = tenantIds;
	}
}
