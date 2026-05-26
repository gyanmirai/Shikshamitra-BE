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
var db = await GetDbContextAsync();

var storedTenantId = currentTenant.Id ?? ReceiptNumberSequence.HostTenantSentinel;
var storedKind = kind.HasValue ? (int)kind.Value : ReceiptNumberSequence.LegacyKindSentinel;
var newId = Guid.NewGuid();

// Single atomic statement:
//   INSERT initialises NextValue = 2 for the first call (receipt #1 returned).
//   ON CONFLICT DO UPDATE increments an existing row atomically.
//   RETURNING NextValue - 1 so the first issued number is always 1.
// PostgreSQL row-level lock on the UPDATE phase serialises concurrent callers.
var sequenceValue = (await db.Database
    .SqlQuery<long>(
        $"""
        INSERT INTO "FinanceReceiptNumberSequences"
            ("Id", "TenantId", "Year", "Kind", "NextValue")
        VALUES ({newId}, {storedTenantId}, {year}, {storedKind}, 2)
        ON CONFLICT ("TenantId", "Year", "Kind") DO UPDATE
            SET "NextValue" = "FinanceReceiptNumberSequences"."NextValue" + 1
        RETURNING "FinanceReceiptNumberSequences"."NextValue" - 1 AS "Value"
        """)
    .ToListAsync(cancellationToken))
    .First();

return sequenceValue;
}
}
