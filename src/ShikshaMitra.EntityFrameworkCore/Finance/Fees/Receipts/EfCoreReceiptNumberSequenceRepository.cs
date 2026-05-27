using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Receipts;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.Receipts;

/// <summary>
/// EF Core (PostgreSQL) implementation of <see cref="IReceiptNumberSequenceRepository"/>.
/// Uses a single INSERT...ON CONFLICT DO UPDATE...RETURNING statement that is both
/// idempotent (creates the row on first call) and concurrent-safe (PostgreSQL
/// row-level lock serialises concurrent increments on the same sequence row).
/// Sentinel values (Guid.Empty / -1) replace NULLs in the unique index columns.
/// </summary>
public class EfCoreReceiptNumberSequenceRepository(
IDbContextProvider<ShikshaMitraDbContext> dbContextProvider,
ICurrentTenant currentTenant
) : EfCoreRepository<ShikshaMitraDbContext, ReceiptNumberSequence, Guid>(dbContextProvider),
IReceiptNumberSequenceRepository
{
    /// <inheritdoc />
    public async Task<long> IncrementAndGetAsync(
        int year,
        FeeReceiptKind? kind,
        CancellationToken cancellationToken = default)
    {
        var storedTenantId = currentTenant.Id ?? ReceiptNumberSequence.HostTenantSentinel;
        var storedKind = kind.HasValue ? (int)kind.Value : ReceiptNumberSequence.LegacyKindSentinel;

        for (var retry = 0; retry < 10; retry++)
        {
            try
            {
                var dbContext = await GetDbContextAsync();

                var sequence = await dbContext.Set<ReceiptNumberSequence>()
                    .FirstOrDefaultAsync(s =>
                        s.StoredTenantId == storedTenantId &&
                        s.Year == year &&
                        s.StoredKind == storedKind,
                        cancellationToken);

                if (sequence == null)
                {
                    sequence = ReceiptNumberSequence.New(Guid.NewGuid(), currentTenant.Id, year, kind);
                    await dbContext.Set<ReceiptNumberSequence>().AddAsync(sequence, cancellationToken);

                    var returnValue = sequence.NextValue; // 1
                    sequence.Increment(); // NextValue becomes 2

                    await dbContext.SaveChangesAsync(cancellationToken);
                    return returnValue;
                }
                else
                {
                    var returnValue = sequence.NextValue;
                    sequence.Increment();

                    await dbContext.SaveChangesAsync(cancellationToken);
                    return returnValue;
                }
            }
            catch (Exception ex) when (ex is DbUpdateConcurrencyException or DbUpdateException)
            {
                var dbContext = await GetDbContextAsync();
                dbContext.ChangeTracker.Clear();

                if (retry == 9)
                {
                    throw;
                }

                await Task.Delay(5, cancellationToken);
            }
        }

        throw new InvalidOperationException("Failed to generate receipt number sequence due to high concurrency.");
    }
}
