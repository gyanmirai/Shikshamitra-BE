using System.Threading.Tasks;

namespace ShikshaMitra.Data;

public interface IShikshaMitraDbSchemaMigrator
{
    Task MigrateAsync();
}
