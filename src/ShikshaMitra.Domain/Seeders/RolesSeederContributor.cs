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
				continue;
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
