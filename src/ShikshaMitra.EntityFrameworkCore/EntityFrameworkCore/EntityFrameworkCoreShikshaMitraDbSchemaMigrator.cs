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

		var dbContext = serviceProvider
			.GetRequiredService<ShikshaMitraDbContext>();

		// Ensure the public schema exists before running migrations.
		// PostgreSQL requires a valid schema in the search_path to create tables
		// (including the __EFMigrationsHistory table).
		await dbContext.Database.ExecuteSqlRawAsync(
			"CREATE SCHEMA IF NOT EXISTS public;");

		await dbContext.Database.MigrateAsync();
	}
}