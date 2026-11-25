using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms.Services;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.Settings.PaymentTerms;

public class PaymentTermReferenceLock(IDbContextProvider<ShikshaMitraDbContext> contexts, ICurrentTenant tenant)
	: IPaymentTermReferenceLock, ITransientDependency
{
	public async Task<bool> AcquireAsync(Guid[] paymentTermIds)
	{
		if (paymentTermIds.Length == 0) return true;
		var ids = paymentTermIds.Distinct().ToArray();
		var db = await contexts.GetDbContextAsync();
		if (!db.Database.IsNpgsql())
			return await db.Set<PaymentTerm>().CountAsync(term => Enumerable.Contains(ids, term.Id)) == ids.Length;

		if (db.Database.CurrentTransaction == null)
			throw new InvalidOperationException("Payment term reference writes require a transaction.");

		// Keep references and soft deletion mutually exclusive until their transactions commit.
		var locked = await db.Database.SqlQuery<Guid>($"""
			SELECT "Id" AS "Value" FROM "Finance_PaymentTerms"
			WHERE "Id" = ANY({ids}) AND "TenantId" IS NOT DISTINCT FROM {tenant.Id} AND NOT "IsDeleted"
			ORDER BY "Id" FOR UPDATE
			""").ToListAsync();
		return locked.Count == ids.Length;
	}
}
