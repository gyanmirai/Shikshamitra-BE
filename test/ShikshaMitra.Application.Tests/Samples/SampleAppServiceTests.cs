using Shouldly;
using System.Threading.Tasks;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Xunit;

namespace ShikshaMitra.Samples;

/* This is just an example test class.
 * Normally, you don't test code of the modules you are using
 * (like IIdentityUserAppService here).
 * Only test your own application services.
 */
public abstract class SampleAppServiceTests<TStartupModule> : ShikshaMitraApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IIdentityUserAppService _userAppService;

    protected SampleAppServiceTests()
    {
        _userAppService = GetRequiredService<IIdentityUserAppService>();
    }

    [Fact]
    public virtual async Task Initial_Data_Should_Contain_Admin_User()
    {
        var result = await _userAppService.GetListAsync(new GetIdentityUsersInput());

        result.TotalCount.ShouldBeGreaterThan(0);
    }
}
