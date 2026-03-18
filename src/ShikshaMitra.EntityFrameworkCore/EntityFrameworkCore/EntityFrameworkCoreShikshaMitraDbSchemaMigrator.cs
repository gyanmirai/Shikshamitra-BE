using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Data;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.EntityFrameworkCore;

public class EntityFrameworkCoreShikshaMitraDbSchemaMigrator(IServiceProvider serviceProvider)
	: IShikshaMitraDbSchemaMigrator, ITransientDependency
{
	public async Task MigrateAsync()
	{
		/* We intentionally resolve the ShikshaMitraDbContext
         * from IServiceProvider (instead of directly injecting it)
         * to properly get the connection string of the current tenant in the
         * current scope.
         */



		await serviceProvider
			.GetRequiredService<ShikshaMitraDbContext>()
			.Database
			.MigrateAsync();
	}
}