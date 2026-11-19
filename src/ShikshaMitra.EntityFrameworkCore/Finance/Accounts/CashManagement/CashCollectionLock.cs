using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Accounts.CashManagement;

/// <summary>All allocations to an instrument share a PostgreSQL transaction lock.</summary>
public class CashCollectionLock(IDbContextProvider<ShikshaMitraDbContext> contexts, ICurrentTenant tenant)
	: ICashCollectionLock, ITransientDependency
{
	public async Task AcquireAsync(Guid sourceId)
	{
		var db = await contexts.GetDbContextAsync();
		if (!db.Database.IsNpgsql()) return;
		if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Cash commands require a transactional unit of work.");
		var key = BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes($"finance-cash:{tenant.Id}:{sourceId}")), 0);
		await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})");
	}
}
