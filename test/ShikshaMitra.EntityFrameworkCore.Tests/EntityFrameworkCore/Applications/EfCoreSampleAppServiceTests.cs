using ShikshaMitra.Samples;
using Shouldly;
using System.Threading.Tasks;
using Volo.Abp.Identity;
using Volo.Abp.Data;
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

		var roleDto = await roleAppService.CreateAsync(input);
		roleDto.GetProperty<string>("color").ShouldBe("#123456");

		// Also check from DB
		var roleRepository = GetRequiredService<IIdentityRoleRepository>();
		var roleFromDb = await roleRepository.FindByNormalizedNameAsync("TESTCOLORROLE");
		roleFromDb.ShouldNotBeNull();
		roleFromDb.GetProperty<string>("color").ShouldBe("#123456");
	}
}
