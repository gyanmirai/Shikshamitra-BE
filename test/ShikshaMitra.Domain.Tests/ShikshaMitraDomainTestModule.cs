using Volo.Abp.Modularity;

namespace ShikshaMitra;

[DependsOn(
    typeof(ShikshaMitraDomainModule),
    typeof(ShikshaMitraTestBaseModule)
)]
public class ShikshaMitraDomainTestModule : AbpModule
{

}
