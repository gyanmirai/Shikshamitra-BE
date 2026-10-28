using System;
using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms.Services;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.Settings.PaymentTerms;

public class PaymentTermNameMutationLock(
	IDbContextProvider<ShikshaMitraDbContext> contexts, ICurrentTenant tenant)
	: IPaymentTermNameMutationLock, ITransientDependency
{
	public async Task AcquireAsync(Guid academicYearId, CancellationToken cancellationToken = default)
	{
		var context = await contexts.GetDbContextAsync();
		if (!context.Database.IsNpgsql()) return;
		var transaction = context.Database.CurrentTransaction;
		if (transaction == null || transaction.GetDbTransaction().IsolationLevel != IsolationLevel.ReadCommitted)
			throw new InvalidOperationException("Payment term name changes require a Read Committed transaction.");

		// Transaction-scoped and shared across application instances. A name check after
		// acquisition sees the previous writer's commit; release cannot precede persistence.
		var digest = SHA256.HashData(Encoding.UTF8.GetBytes(
			$"finance-payment-term-name:{tenant.Id}:{academicYearId}"));
		var key = BinaryPrimitives.ReadInt64BigEndian(digest);
		await context.Database.ExecuteSqlInterpolatedAsync(
			$"SELECT pg_advisory_xact_lock({key})", cancellationToken);
	}
}
