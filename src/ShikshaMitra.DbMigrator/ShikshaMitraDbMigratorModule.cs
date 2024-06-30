using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace ShikshaMitra.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(ShikshaMitraEntityFrameworkCoreModule),
    typeof(ShikshaMitraApplicationContractsModule)
    )]
public class ShikshaMitraDbMigratorModule : AbpModule
{
}
