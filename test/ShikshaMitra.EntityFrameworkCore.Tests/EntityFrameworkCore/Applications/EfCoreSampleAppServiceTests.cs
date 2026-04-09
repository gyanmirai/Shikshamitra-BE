using ShikshaMitra.Samples;
using Shouldly;
using System.Threading.Tasks;
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
}
