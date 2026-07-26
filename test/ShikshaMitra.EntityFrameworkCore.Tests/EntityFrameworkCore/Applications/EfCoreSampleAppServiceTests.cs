using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Samples;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Applications;

[Collection(ShikshaMitraTestConsts.CollectionDefinitionName)]
public class EfCoreSampleAppServiceTests : SampleAppServiceTests<ShikshaMitraEntityFrameworkCoreTestModule>
{
	[Fact]
	public override async Task Initial_Data_Should_Contain_Admin_User()
	{
		var userAppService = GetRequiredService<IIdentityUserAppService>();
		var result = await userAppService.GetListAsync(new GetIdentityUsersInput());

		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task Should_Create_Role_With_Color()
	{
		var roleAppService = GetRequiredService<IIdentityRoleAppService>();
		var input = new IdentityRoleCreateDto
		{
			Name = "TestColorRole",
			IsDefault = true,
			IsPublic = true
		};
		input.SetProperty("color", "#123456");
		input.SetProperty(EmployeeRoleAudience.ExtraPropertyName, EmployeeRoleAudience.Employee);
		input.SetProperty(EmployeeGroupAudience.ExtraPropertyName, EmployeeGroupAudience.Both);
		input.SetProperty(EmployeeOnboardingRoleAssignment.ExtraPropertyName, false);

		var roleDto = await roleAppService.CreateAsync(input);
		roleDto.GetProperty<string>("color").ShouldBe("#123456");

		// Also check from DB
		var roleRepository = GetRequiredService<IIdentityRoleRepository>();
		var roleFromDb = await roleRepository.FindByNormalizedNameAsync("TESTCOLORROLE");
		roleFromDb.ShouldNotBeNull();
		roleFromDb.GetProperty<string>("color").ShouldBe("#123456");
	}

	[Fact]
	public async Task Should_Round_Trip_Employee_Group_Audience_On_Role_Create_And_Update()
	{
		var roleAppService = GetRequiredService<IIdentityRoleAppService>();
		var roleName = $"EmployeeGroupRole-{Guid.NewGuid():N}";
		var input = new IdentityRoleCreateDto
		{
			Name = roleName,
			IsDefault = false,
			IsPublic = false
		};
		input.SetProperty(EmployeeRoleAudience.ExtraPropertyName, EmployeeRoleAudience.Employee);
		input.SetProperty(
			EmployeeGroupAudience.ExtraPropertyName,
			EmployeeGroupAudience.Teaching);
		input.SetProperty(EmployeeOnboardingRoleAssignment.ExtraPropertyName, true);

		var created = await roleAppService.CreateAsync(input);

		created.GetProperty<string>(EmployeeRoleAudience.ExtraPropertyName)
			.ShouldBe(EmployeeRoleAudience.Employee);
		created.GetProperty<string>(EmployeeGroupAudience.ExtraPropertyName)
			.ShouldBe(EmployeeGroupAudience.Teaching);
		created.GetProperty<bool>(EmployeeOnboardingRoleAssignment.ExtraPropertyName)
			.ShouldBeTrue();

		var update = new IdentityRoleUpdateDto
		{
			Name = roleName,
			IsDefault = false,
			IsPublic = false
		};
		update.SetProperty(EmployeeRoleAudience.ExtraPropertyName, EmployeeRoleAudience.Employee);
		update.SetProperty(
			EmployeeGroupAudience.ExtraPropertyName,
			EmployeeGroupAudience.NonTeaching);
		update.SetProperty(EmployeeOnboardingRoleAssignment.ExtraPropertyName, false);

		var updated = await roleAppService.UpdateAsync(created.Id, update);

		updated.GetProperty<string>(EmployeeRoleAudience.ExtraPropertyName)
			.ShouldBe(EmployeeRoleAudience.Employee);
		updated.GetProperty<string>(EmployeeGroupAudience.ExtraPropertyName)
			.ShouldBe(EmployeeGroupAudience.NonTeaching);
		updated.GetProperty<bool>(EmployeeOnboardingRoleAssignment.ExtraPropertyName)
			.ShouldBeFalse();
		var roleRepository = GetRequiredService<IIdentityRoleRepository>();
		var roleFromDb = await roleRepository.FindByNormalizedNameAsync(roleName.ToUpperInvariant());
		roleFromDb.ShouldNotBeNull();
		roleFromDb.GetProperty<string>(EmployeeRoleAudience.ExtraPropertyName)
			.ShouldBe(EmployeeRoleAudience.Employee);
		roleFromDb.GetProperty<string>(EmployeeGroupAudience.ExtraPropertyName)
			.ShouldBe(EmployeeGroupAudience.NonTeaching);
		roleFromDb.GetProperty<bool>(EmployeeOnboardingRoleAssignment.ExtraPropertyName)
			.ShouldBeFalse();
	}
}
