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
                .AddOrUpdateProperty<IdentityRoleUpdateDto, string>("color");
        });
    }
}
