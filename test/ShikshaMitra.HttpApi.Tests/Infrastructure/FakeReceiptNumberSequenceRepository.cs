using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.Receipts;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Infrastructure;

/// <summary>
/// SQLite-safe replacement for <c>EfCoreReceiptNumberSequenceRepository</c>.
/// The production repo uses a PostgreSQL-specific upsert with RETURNING that
/// crashes on SQLite. This fake replaces only <see cref="IncrementAndGetAsync"/>
/// with a thread-safe in-memory counter; all other IRepository members delegate
/// to the EfCoreRepository base class as normal.
/// </summary>
internal sealed class FakeReceiptNumberSequenceRepository(
    IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
    : EfCoreRepository<ShikshaMitraDbContext, ReceiptNumberSequence, Guid>(dbContextProvider),
      IReceiptNumberSequenceRepository
{
    private static readonly ConcurrentDictionary<string, long> _sequences = new();

    public Task<long> IncrementAndGetAsync(int year, FeeReceiptKind? kind, CancellationToken ct = default)
    {
        var key = $"{year}:{kind?.ToString() ?? "null"}";
        var next = _sequences.AddOrUpdate(key, 1L, (_, current) => current + 1);
        return Task.FromResult(next);
    }
}
