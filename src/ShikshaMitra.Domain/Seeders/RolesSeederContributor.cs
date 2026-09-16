using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public class RolesSeederContributor : BaseSeederContributor
{
	internal const string StepName = "RolesSeeder";
	internal const string StepVersion = "RolesSeeder:v5";
	internal const int StepOrder = 110;
	internal const string AdminRoleName = "admin";
	internal const string SuperAdminRoleName = "super-admin";
	private const string HeadOfDepartmentRoleName = "Head of Department";
	internal const string HodRoleName = "HOD";

	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IPermissionGrantSeedRepository _permissionGrants;

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
		{ "Office Assistant", "#388e3c" },
		{ "Admissions Receptionist", "#00897b" },
		{ "Admissions Counselor", "#1565c0" },
		{ "Admissions Approver", "#6a1b9a" }
	};

	public RolesSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork unitOfWork,
		IIdentityRoleRepository roleRepository,
		IPermissionGrantSeedRepository permissionGrants)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_roleRepository = roleRepository;
		_permissionGrants = permissionGrants;
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
			new IdentityRole(GuidGenerator.Create(), "Office Assistant", tenantId),
			new IdentityRole(GuidGenerator.Create(), "Admissions Receptionist", tenantId),
			new IdentityRole(GuidGenerator.Create(), "Admissions Counselor", tenantId),
			new IdentityRole(GuidGenerator.Create(), "Admissions Approver", tenantId)
		};

		foreach (var role in roles)
		{
			var existingRole = await _roleRepository.FindByNormalizedNameAsync(role.NormalizedName);
			if (existingRole != null)
			{
				var changed = false;
				if (string.IsNullOrEmpty(existingRole.GetProperty<string>("color")) && DefaultRoleColors.TryGetValue(existingRole.Name, out var col))
				{
					existingRole.SetProperty("color", col);
					changed = true;
				}

				var existingAudience = existingRole.GetProperty<string>(
					EmployeeRoleAudience.ExtraPropertyName);
				if (string.IsNullOrWhiteSpace(existingAudience))
				{
					existingRole.SetProperty(
						EmployeeRoleAudience.ExtraPropertyName,
						RoleEmployeeAudienceDefaults.ResolveRoleAudience(
							existingRole.Name,
							existingAudience));
					changed = true;
				}

				var existingEmployeeGroupAudience = existingRole.GetProperty<string>(
					EmployeeGroupAudience.ExtraPropertyName);
				if (string.IsNullOrWhiteSpace(existingEmployeeGroupAudience))
				{
					existingRole.SetProperty(
						EmployeeGroupAudience.ExtraPropertyName,
						RoleEmployeeAudienceDefaults.ResolveEmployeeGroupAudience(
							existingRole.Name,
							existingEmployeeGroupAudience));
					changed = true;
				}

				var hasEmployeeOnboardingMarker = existingRole.ExtraProperties.ContainsKey(
					EmployeeOnboardingRoleAssignment.ExtraPropertyName);
				bool? existingEmployeeOnboardingMarker = hasEmployeeOnboardingMarker
					? existingRole.GetProperty<bool>(EmployeeOnboardingRoleAssignment.ExtraPropertyName)
					: null;
				var employeeOnboardingAssignable =
					RoleEmployeeAudienceDefaults.ResolveEmployeeOnboardingAssignable(
						existingRole.Name,
						existingEmployeeOnboardingMarker);
				if (!hasEmployeeOnboardingMarker ||
					existingEmployeeOnboardingMarker != employeeOnboardingAssignable)
				{
					existingRole.SetProperty(
						EmployeeOnboardingRoleAssignment.ExtraPropertyName,
						employeeOnboardingAssignable);
					changed = true;
				}

				if (changed)
				{
					await _roleRepository.UpdateAsync(existingRole);
				}
				continue;
			}

			if (DefaultRoleColors.TryGetValue(role.Name, out var color))
			{
				role.SetProperty("color", color);
			}
			role.SetProperty(
				EmployeeRoleAudience.ExtraPropertyName,
				EmployeeRoleAudience.ForRoleName(role.Name));
			role.SetProperty(
				EmployeeGroupAudience.ExtraPropertyName,
				RoleEmployeeAudienceDefaults.ResolveEmployeeGroupAudience(role.Name, null));
			role.SetProperty(
				EmployeeOnboardingRoleAssignment.ExtraPropertyName,
				RoleEmployeeAudienceDefaults.ResolveEmployeeOnboardingAssignable(role.Name, null));

			await _roleRepository.InsertAsync(role);
			// Creation and defaults share this step's transaction. Existing roles are never repaired.
			await _permissionGrants.InsertRoleGrantsIgnoringDuplicatesAsync(role.Name,
				RolePermissionSeederContributor.GetInitialPermissions(role.Name, tenantId), tenantId);
		}

		await BackfillCustomEmployeeRoleGroupAudiencesAsync();
		await CurrentUnitOfWork.SaveChangesAsync();
	}

	private async Task BackfillCustomEmployeeRoleGroupAudiencesAsync()
	{
		var roles = await _roleRepository.GetListAsync();
		foreach (var role in roles)
		{
			if (!role.ExtraProperties.ContainsKey(
				EmployeeOnboardingRoleAssignment.ExtraPropertyName))
			{
				role.SetProperty(
					EmployeeOnboardingRoleAssignment.ExtraPropertyName,
					RoleEmployeeAudienceDefaults.ResolveEmployeeOnboardingAssignable(
						role.Name,
						null));
				await _roleRepository.UpdateAsync(role);
			}

			var existingGroupAudience = role.GetProperty<string>(
				EmployeeGroupAudience.ExtraPropertyName);
			if (!string.IsNullOrWhiteSpace(existingGroupAudience))
			{
				continue;
			}

			var roleAudience = role.GetProperty<string>(EmployeeRoleAudience.ExtraPropertyName);
			if (!EmployeeRoleAudience.IsEmployeeAssignable(roleAudience, role.Name))
			{
				continue;
			}

			role.SetProperty(
				EmployeeGroupAudience.ExtraPropertyName,
				EmployeeGroupAudience.Both);
			await _roleRepository.UpdateAsync(role);
		}
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
