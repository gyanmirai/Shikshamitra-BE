using Volo.Abp.Modularity;

namespace ShikshaMitra;

[DependsOn(
    typeof(ShikshaMitraApplicationModule),
    typeof(ShikshaMitraDomainTestModule)
)]
public class ShikshaMitraApplicationTestModule : AbpModule
{

}
