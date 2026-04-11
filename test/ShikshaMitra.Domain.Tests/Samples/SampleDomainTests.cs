using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Xunit;

namespace ShikshaMitra.Samples;

/* This is just an example test class.
 * Normally, you don't test code of the modules you are using
 * (like IdentityUserManager here).
 * Only test your own domain services.
 */
public abstract class SampleDomainTests<TStartupModule> : ShikshaMitraDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IIdentityUserRepository _identityUserRepository;
    private readonly IdentityUserManager _identityUserManager;

    protected SampleDomainTests()
    {
        _identityUserRepository = GetRequiredService<IIdentityUserRepository>();
        _identityUserManager = GetRequiredService<IdentityUserManager>();
    }

    [Fact]
    public virtual async Task Should_Set_Email_Of_A_User()
    {
        IdentityUser? targetUser = null;

        await WithUnitOfWorkAsync(async () =>
        {
            targetUser = await _identityUserRepository
                .FindByNormalizedUserNameAsync("ADMIN");

            targetUser ??= (await _identityUserRepository.GetListAsync(maxResultCount: 1)).FirstOrDefault();

            targetUser.ShouldNotBeNull();

            await _identityUserManager.SetEmailAsync(targetUser!, "newemail@abp.io");
            await _identityUserRepository.UpdateAsync(targetUser!);
        });

        targetUser = await _identityUserRepository
            .FindByNormalizedUserNameAsync(targetUser!.NormalizedUserName!);
        targetUser.Email.ShouldBe("newemail@abp.io");
    }
}
