using System;
using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.Settings.FeeStructures;

public class FeeStructureRevisionLock(
	IDbContextProvider<ShikshaMitraDbContext> contexts, ICurrentTenant tenant)
	: IFeeStructureRevisionLock, ITransientDependency
{
	public async Task AcquireAsync(Guid revisionRootId, CancellationToken cancellationToken = default)
	{
		var context = await contexts.GetDbContextAsync();
		if (!context.Database.IsNpgsql()) return;
		var transaction = context.Database.CurrentTransaction;
		if (transaction == null || transaction.GetDbTransaction().IsolationLevel != IsolationLevel.ReadCommitted)
			throw new InvalidOperationException("Fee structure revision changes require a Read Committed transaction.");

		var digest = SHA256.HashData(Encoding.UTF8.GetBytes(
			$"finance-fee-structure-revision:{tenant.Id}:{revisionRootId}"));
		var key = BinaryPrimitives.ReadInt64BigEndian(digest);
		// Hold through commit so the next writer sees reserved revision numbers and request IDs.
		await context.Database.ExecuteSqlInterpolatedAsync(
			$"SELECT pg_advisory_xact_lock({key})", cancellationToken);
	}
}
