using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Seeders;
using Volo.Abp.Data;
using Volo.Abp.Domain.Services;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using IdentityRole = Volo.Abp.Identity.IdentityRole;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace ShikshaMitra.HostTenants;

public class TenantIdentityInitializer(
	IdentityRoleManager roles, IdentityUserManager users, IPermissionGrantSeedRepository grants,
	ICurrentTenant currentTenant, IUnitOfWorkManager unitOfWorkManager) : DomainService
{
	public async Task InitializeAsync(Guid tenantId, string email, string password)
	{
		using (currentTenant.Change(tenantId))
		{
			foreach (var roleName in RolePermissionSeederContributor.BuildRolePermissions().Keys)
			{
				if (await roles.FindByNameAsync(roleName) != null) continue;
				var role = new IdentityRole(GuidGenerator.Create(), roleName, tenantId);
				role.SetProperty(EmployeeRoleAudience.ExtraPropertyName, EmployeeRoleAudience.ForRoleName(roleName));
				role.SetProperty(EmployeeGroupAudience.ExtraPropertyName, RoleEmployeeAudienceDefaults.ResolveEmployeeGroupAudience(roleName, null));
				role.SetProperty(EmployeeOnboardingRoleAssignment.ExtraPropertyName, RoleEmployeeAudienceDefaults.ResolveEmployeeOnboardingAssignable(roleName, null));
				(await roles.CreateAsync(role)).CheckErrors();
				await grants.InsertRoleGrantsIgnoringDuplicatesAsync(role.Name,
					RolePermissionSeederContributor.GetInitialPermissions(role.Name, tenantId), tenantId);
			}
			var user = new IdentityUser(GuidGenerator.Create(), "admin", email.Trim(), tenantId);
			(await users.CreateAsync(user, password)).CheckErrors();
			(await users.AddToRoleAsync(user, "admin")).CheckErrors();
			await unitOfWorkManager.Current!.SaveChangesAsync();
		}
	}
}
