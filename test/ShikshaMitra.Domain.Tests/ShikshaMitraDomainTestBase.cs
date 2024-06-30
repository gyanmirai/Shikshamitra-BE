using Volo.Abp.Modularity;

namespace ShikshaMitra;

/* Inherit from this class for your domain layer tests. */
public abstract class ShikshaMitraDomainTestBase<TStartupModule> : ShikshaMitraTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
