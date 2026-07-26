using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.Identity;

namespace ShikshaMitra.HR.Employees;

internal static class EmployeeRoleTestHelper
{
	public static async Task<Guid> EnsureAssignableRoleAsync(
		IIdentityRoleRepository roleRepository,
		IGuidGenerator guidGenerator,
		Guid? tenantId,
		string roleNamePrefix)
	{
		var existing = (await roleRepository.GetListAsync()).FirstOrDefault(role =>
			string.Equals(
				role.GetProperty<string>(EmployeeRoleAudience.ExtraPropertyName),
				EmployeeRoleAudience.Employee,
				StringComparison.Ordinal) &&
			role.GetProperty<bool>(EmployeeOnboardingRoleAssignment.ExtraPropertyName) &&
			!EmployeeOnboardingRoleAssignment.IsReservedSystemRole(role.Name) &&
			EmployeeGroupAudience.IsAssignableTo(
				role.GetProperty<string>(EmployeeGroupAudience.ExtraPropertyName),
				EmployeeGroupType.NonTeaching));
		if (existing != null)
		{
			return existing.Id;
		}

		var id = guidGenerator.Create();
		var role = new IdentityRole(id, $"{roleNamePrefix}-{id:N}", tenantId);
		role.SetProperty(EmployeeRoleAudience.ExtraPropertyName, EmployeeRoleAudience.Employee);
		role.SetProperty(EmployeeGroupAudience.ExtraPropertyName, EmployeeGroupAudience.Both);
		role.SetProperty(EmployeeOnboardingRoleAssignment.ExtraPropertyName, true);
		await roleRepository.InsertAsync(role, autoSave: true);
		return id;
	}
}
