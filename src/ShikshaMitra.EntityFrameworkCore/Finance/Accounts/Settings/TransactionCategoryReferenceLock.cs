using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Accounts.Settings;

public class TransactionCategoryReferenceLock(
	IDbContextProvider<ShikshaMitraDbContext> contexts, ICurrentTenant tenant)
	: ITransactionCategoryReferenceLock, ITransientDependency
{
	public async Task AcquireAsync()
	{
		var context = await contexts.GetDbContextAsync();
		if (!context.Database.IsNpgsql()) return;
		if (context.Database.CurrentTransaction == null)
			throw new InvalidOperationException("Category reference changes require a transactional unit of work.");
		// A common tenant lock also covers nested Fee Category commands with multiple heads,
		// avoiding category-order deadlocks. PostgreSQL releases it only at transaction end.
		var digest = SHA256.HashData(Encoding.UTF8.GetBytes($"finance-category-reference:{tenant.Id}"));
		var key = BitConverter.ToInt64(digest, 0);
		await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})");
	}
}
