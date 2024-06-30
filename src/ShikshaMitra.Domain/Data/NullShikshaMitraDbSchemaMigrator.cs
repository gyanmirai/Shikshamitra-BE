using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Data;

/* This is used if database provider does't define
 * IShikshaMitraDbSchemaMigrator implementation.
 */
public class NullShikshaMitraDbSchemaMigrator : IShikshaMitraDbSchemaMigrator, ITransientDependency
{
    public Task MigrateAsync()
    {
        return Task.CompletedTask;
    }
}
