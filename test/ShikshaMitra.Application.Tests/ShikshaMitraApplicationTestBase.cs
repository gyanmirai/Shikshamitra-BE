using Volo.Abp.Modularity;

namespace ShikshaMitra;

public abstract class ShikshaMitraApplicationTestBase<TStartupModule> : ShikshaMitraTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
