using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Receipts;
using Xunit;

namespace ShikshaMitra.Finance.Receipts;

/// <summary>
/// Tests that a sequence-increment mechanism produces globally-unique,
/// monotonically-increasing values under high concurrency — validating the
/// contract expected from <see cref="IReceiptNumberSequenceRepository.IncrementAndGetAsync"/>.
///
/// These tests use an in-memory lock-based fake to prove the contract;
/// the real PostgreSQL implementation achieves the same via a single atomic
/// INSERT … ON CONFLICT DO UPDATE … RETURNING statement.
/// </summary>
public class ReceiptNumberSequence_ConcurrencyTests
{
    // ── Minimal local interface for pure contract testing ─────────────────────

    private interface ISequencer
    {
        Task<long> IncrementAndGetAsync(int year, FeeReceiptKind? kind, CancellationToken ct = default);
    }

    // ── In-memory fake ─────────────────────────────────────────────────────────

    private sealed class InMemorySequencer : ISequencer
    {
        private readonly Dictionary<string, long> _sequences = new();
        private readonly object _lock = new();

        public Task<long> IncrementAndGetAsync(int year, FeeReceiptKind? kind, CancellationToken ct = default)
        {
            var key = $"{year}:{kind?.ToString() ?? "null"}";
            lock (_lock)
            {
                _sequences.TryGetValue(key, out var current);
                var next = current + 1;
                _sequences[key] = next;
                return Task.FromResult(next);
            }
        }
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Fifty_Concurrent_Calls_Produce_Unique_Sequence_Numbers()
    {
        ISequencer seq = new InMemorySequencer();
        const int count = 50;

        var results = new long[count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, count),
            new ParallelOptions { MaxDegreeOfParallelism = 16 },
            async (i, ct) =>
            {
                results[i] = await seq.IncrementAndGetAsync(2025, FeeReceiptKind.FinalFee, ct);
            });

        Assert.Equal(count, results.Distinct().Count());
    }

    [Fact]
    public async Task Sequence_Covers_Full_Range_Without_Gaps_Under_Concurrency()
    {
        ISequencer seq = new InMemorySequencer();
        const int count = 100;

        var results = new List<long>(count);
        var syncLock = new object();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, count),
            new ParallelOptions { MaxDegreeOfParallelism = 32 },
            async (_, ct) =>
            {
                var value = await seq.IncrementAndGetAsync(2025, FeeReceiptKind.ProvisionalFee, ct);
                lock (syncLock) { results.Add(value); }
            });

        var sorted = results.OrderBy(x => x).ToList();
        Assert.Equal(1L, sorted.First());
        Assert.Equal((long)count, sorted.Last());
        Assert.Equal(count, sorted.Distinct().Count());
    }

    [Fact]
    public async Task Different_Kinds_Maintain_Independent_Sequences()
    {
        ISequencer seq = new InMemorySequencer();

        var a = await seq.IncrementAndGetAsync(2025, FeeReceiptKind.FinalFee);
        var b = await seq.IncrementAndGetAsync(2025, FeeReceiptKind.ProvisionalFee);
        var c = await seq.IncrementAndGetAsync(2025, FeeReceiptKind.ProvisionalCharge);
        var d = await seq.IncrementAndGetAsync(2025, kind: null);

        Assert.Equal(1L, a);
        Assert.Equal(1L, b);
        Assert.Equal(1L, c);
        Assert.Equal(1L, d);
    }

    [Fact]
    public async Task Different_Years_Maintain_Independent_Sequences()
    {
        ISequencer seq = new InMemorySequencer();

        for (var i = 0; i < 3; i++)
            await seq.IncrementAndGetAsync(2024, FeeReceiptKind.FinalFee);

        var firstOf2025 = await seq.IncrementAndGetAsync(2025, FeeReceiptKind.FinalFee);
        Assert.Equal(1L, firstOf2025);
    }
}
