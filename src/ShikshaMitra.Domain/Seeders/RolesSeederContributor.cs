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
		var roles = new List<IdentityRole>
		{
			// Core System Roles
			new IdentityRole(GuidGenerator.Create(), "Director", tenantId),
			new IdentityRole(GuidGenerator.Create(), "Principal", tenantId),
			new IdentityRole(GuidGenerator.Create(), "Teacher", tenantId),
			new IdentityRole(GuidGenerator.Create(), "Clerk", tenantId),
			new IdentityRole(GuidGenerator.Create(), "Student", tenantId),
			new IdentityRole(GuidGenerator.Create(), "Parent", tenantId),

			// Department Heads
			new IdentityRole(GuidGenerator.Create(), "Head of Department", tenantId),

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
			await _roleRepository.InsertAsync(role);
		}
	}
}
