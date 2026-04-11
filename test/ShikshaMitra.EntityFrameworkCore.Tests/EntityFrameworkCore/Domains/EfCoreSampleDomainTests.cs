using ShikshaMitra.Samples;
using Shouldly;
using System.Threading.Tasks;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Domains;

[Collection(ShikshaMitraTestConsts.CollectionDefinitionName)]
public class EfCoreSampleDomainTests : SampleDomainTests<ShikshaMitraEntityFrameworkCoreTestModule>
{
	[Fact]
	public override async Task Should_Set_Email_Of_A_User()
	{
		var userManager = GetRequiredService<IdentityUserManager>();
		var userRepository = GetRequiredService<IIdentityUserRepository>();

		IdentityUser testUser = null!;

		await WithUnitOfWorkAsync(async () =>
		{
			testUser = new IdentityUser(Volo.Abp.Guids.SimpleGuidGenerator.Instance.Create(), "efcoretest", "efcoretest@test.com");
			await userManager.CreateAsync(testUser, "Test@1234");
		});

		await WithUnitOfWorkAsync(async () =>
		{
			var user = await userRepository.FindAsync(testUser.Id);
			user.ShouldNotBeNull();

			await userManager.SetEmailAsync(user!, "newemail@abp.io");
			await userRepository.UpdateAsync(user!);
		});

		var updated = await userRepository.FindAsync(testUser.Id);
		updated!.Email.ShouldBe("newemail@abp.io");
	}
}
