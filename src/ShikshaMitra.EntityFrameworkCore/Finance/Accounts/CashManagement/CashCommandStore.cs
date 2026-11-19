using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Finance.Accounts.CashManagement;

public class CashCommandStore(IDbContextProvider<ShikshaMitraDbContext> contexts)
	: ICashCommandStore, ITransientDependency
{
	public async Task AppendAsync(Guid sourceId, Guid commandId, string fingerprint, string resultJson)
	{
		var db = await contexts.GetDbContextAsync();
		if (db.Database.CurrentTransaction == null)
			throw new InvalidOperationException("Cash replay evidence requires the cash command transaction.");
		// An owned-navigation insertion through the change tracker also rotates the
		// owner's ABP stamp. Append the already-bound response atomically, preserving
		// the cash version returned to callers. The composite PK rejects duplicates.
		await db.Database.ExecuteSqlInterpolatedAsync($"""
			INSERT INTO "Finance_CashCommands" ("CashCollectionId", "Id", "Fingerprint", "ResultJson")
			VALUES ({sourceId}, {commandId}, {fingerprint}, {resultJson})
			""");
	}
}
