using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public class RolesSeederContributor : BaseSeederContributor
{
	private const string AdminRoleName = "admin";
	private const string SuperAdminRoleName = "super-admin";
	private const string HeadOfDepartmentRoleName = "Head of Department";
	private const string HodRoleName = "HOD";

	private readonly IIdentityRoleRepository _roleRepository;

	private static readonly Dictionary<string, string> DefaultRoleColors = new()
	{
		{ "admin", "#d32f2f" },
		{ "super-admin", "#d32f2f" },
		{ "Director", "#9c27b0" },
		{ "Principal", "#7b1fa2" },
		{ "Teacher", "#1976d2" },
		{ "Clerk", "#388e3c" },
		{ "Student", "#0097a7" },
		{ "Parent", "#5d4037" },
		{ "HOD", "#f57c00" },
		{ "Vice Principal", "#7b1fa2" },
		{ "Administrative Manager", "#388e3c" },
		{ "HR Manager", "#c2185b" },
		{ "Finance Officer", "#5d4037" },
		{ "IT Administrator", "#00796b" },
		{ "Office Assistant", "#388e3c" }
	};

	public RolesSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork unitOfWork,
		IIdentityRoleRepository roleRepository)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_roleRepository = roleRepository;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		var tenantId = CurrentTenant.Id;
		await RenameHeadOfDepartmentRoleAsync();

		var roles = new List<IdentityRole>
		{
			// Core System Roles
			new IdentityRole(GuidGenerator.Create(), AdminRoleName, tenantId),
			new IdentityRole(GuidGenerator.Create(), SuperAdminRoleName, tenantId),
			new IdentityRole(GuidGenerator.Create(), "Director", tenantId),
			new IdentityRole(GuidGenerator.Create(), "Principal", tenantId),
			new IdentityRole(GuidGenerator.Create(), "Teacher", tenantId),
			new IdentityRole(GuidGenerator.Create(), "Clerk", tenantId),
			new IdentityRole(GuidGenerator.Create(), "Student", tenantId),
			new IdentityRole(GuidGenerator.Create(), "Parent", tenantId),

			// Department Heads
			new IdentityRole(GuidGenerator.Create(), HodRoleName, tenantId),

			// Administrative Roles
			new IdentityRole(GuidGenerator.Create(), "Vice Principal", tenantId),
			new IdentityRole(GuidGenerator.Create(), "Administrative Manager", tenantId),
			new IdentityRole(GuidGenerator.Create(), "HR Manager", tenantId),
			new IdentityRole(GuidGenerator.Create(), "Finance Officer", tenantId),
			new IdentityRole(GuidGenerator.Create(), "IT Administrator", tenantId),
			new IdentityRole(GuidGenerator.Create(), "Office Assistant", tenantId)
		};

		foreach (var role in roles)
		{
			var existingRole = await _roleRepository.FindByNormalizedNameAsync(role.NormalizedName);
			if (existingRole != null)
			{
				if (string.IsNullOrEmpty(existingRole.GetProperty<string>("color")) && DefaultRoleColors.TryGetValue(existingRole.Name, out var col))
				{
					existingRole.SetProperty("color", col);
					await _roleRepository.UpdateAsync(existingRole);
				}
				continue;
			}

			if (DefaultRoleColors.TryGetValue(role.Name, out var color))
			{
				role.SetProperty("color", color);
			}

			await _roleRepository.InsertAsync(role);
		}

		await CurrentUnitOfWork.SaveChangesAsync();
	}

	private async Task RenameHeadOfDepartmentRoleAsync()
	{
		var existingHodRole = await _roleRepository.FindByNormalizedNameAsync(HodRoleName.ToUpperInvariant());
		if (existingHodRole != null)
		{
			return;
		}

		var existingHeadOfDepartmentRole = await _roleRepository.FindByNormalizedNameAsync(HeadOfDepartmentRoleName.ToUpperInvariant());
		if (existingHeadOfDepartmentRole == null)
		{
			return;
		}

		existingHeadOfDepartmentRole.ChangeName(HodRoleName);
		await _roleRepository.UpdateAsync(existingHeadOfDepartmentRole);
	}
}
