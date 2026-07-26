using Volo.Abp.Identity;
using Volo.Abp.ObjectExtending;
using Volo.Abp.Threading;

namespace ShikshaMitra;

public static class ShikshaMitraDtoExtensions
{
	private static readonly OneTimeRunner OneTimeRunner = new OneTimeRunner();

	public static void Configure()
	{
		OneTimeRunner.Run(() =>
		{
			ObjectExtensionManager.Instance
				.AddOrUpdateProperty<IdentityRoleDto, string>("color")
				.AddOrUpdateProperty<IdentityRoleCreateDto, string>("color")
				.AddOrUpdateProperty<IdentityRoleUpdateDto, string>("color")
				.AddOrUpdateProperty<IdentityRoleDto, string>(HR.Employees.EmployeeRoleAudience.ExtraPropertyName)
				.AddOrUpdateProperty<IdentityRoleCreateDto, string>(HR.Employees.EmployeeRoleAudience.ExtraPropertyName)
				.AddOrUpdateProperty<IdentityRoleUpdateDto, string>(HR.Employees.EmployeeRoleAudience.ExtraPropertyName)
				.AddOrUpdateProperty<IdentityRoleDto, string>(HR.Employees.EmployeeGroupAudience.ExtraPropertyName)
				.AddOrUpdateProperty<IdentityRoleCreateDto, string>(HR.Employees.EmployeeGroupAudience.ExtraPropertyName)
				.AddOrUpdateProperty<IdentityRoleUpdateDto, string>(HR.Employees.EmployeeGroupAudience.ExtraPropertyName)
				.AddOrUpdateProperty<IdentityRoleDto, bool>(HR.Employees.EmployeeOnboardingRoleAssignment.ExtraPropertyName)
				.AddOrUpdateProperty<IdentityRoleCreateDto, bool>(HR.Employees.EmployeeOnboardingRoleAssignment.ExtraPropertyName)
				.AddOrUpdateProperty<IdentityRoleUpdateDto, bool>(HR.Employees.EmployeeOnboardingRoleAssignment.ExtraPropertyName);
		});
	}
}
