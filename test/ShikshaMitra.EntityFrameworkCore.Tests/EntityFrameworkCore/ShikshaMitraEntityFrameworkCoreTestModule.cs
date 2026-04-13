using Volo.Abp.Modularity;

namespace ShikshaMitra.EntityFrameworkCore;

[DependsOn(
    typeof(ShikshaMitraApplicationTestModule),
    typeof(ShikshaMitraEntityFrameworkCoreModule)
    )]
public class ShikshaMitraEntityFrameworkCoreTestModule : AbpModule
{
}
