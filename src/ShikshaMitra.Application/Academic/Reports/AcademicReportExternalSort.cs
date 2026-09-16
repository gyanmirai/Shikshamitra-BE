using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Reports;

// Preserve the report's CLR comparison/culture semantics without retaining all rows to sort them.
internal static class AcademicReportExternalSort
{
    public static async IAsyncEnumerable<T> SortAsync<T>(
        IAsyncEnumerable<T> source, Comparison<T> comparison, int chunkSize = 512,
        [EnumeratorCancellation] CancellationToken cancellationToken = default, string? temporaryDirectoryRoot = null)
    {
        if (chunkSize < 2) throw new ArgumentOutOfRangeException(nameof(chunkSize));
        var directory = Path.Combine(temporaryDirectoryRoot ?? Path.GetTempPath(), $"shikshamitra-report-sort-{Guid.NewGuid():N}");
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(directory);
        else Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var comparer = Comparer<SortRecord<T>>.Create((left, right) =>
        {
            var order = comparison(left.Value, right.Value);
            return order != 0 ? order : left.Sequence.CompareTo(right.Sequence);
        });
        try
        {
            var chunk = new List<SortRecord<T>>(chunkSize);
            long sequence = 0;
            var runCount = 0;
            await foreach (var row in source.WithCancellation(cancellationToken))
            {
                chunk.Add(new SortRecord<T>(sequence++, row));
                if (chunk.Count < chunkSize) continue;
                await WriteRunAsync(RunPath(directory, 0, runCount++), chunk, comparer, cancellationToken);
                chunk.Clear();
            }
            if (chunk.Count > 0)
                await WriteRunAsync(RunPath(directory, 0, runCount++), chunk, comparer, cancellationToken);
            chunk.Clear();

            // A fixed fan-in bounds both memory and open file handles, even for very large reports.
            const int fanIn = 16;
            var pass = 0;
            while (runCount > 1)
            {
                var nextCount = 0;
                for (var start = 0; start < runCount; start += fanIn)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var count = Math.Min(fanIn, runCount - start);
                    await MergeAsync(directory, pass, start, count,
                        RunPath(directory, pass + 1, nextCount++), comparer, cancellationToken);
                    for (var index = start; index < start + count; index++)
                        File.Delete(RunPath(directory, pass, index));
                }
                runCount = nextCount;
                pass++;
            }
            if (runCount == 0) yield break;
            using var reader = new StreamReader(RunPath(directory, pass, 0), Encoding.UTF8);
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return JsonSerializer.Deserialize<SortRecord<T>>(line)!.Value;
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string RunPath(string directory, int pass, int run) => Path.Combine(directory, $"{pass}-{run}.jsonl");

    private static async Task WriteRunAsync<T>(string path, List<SortRecord<T>> chunk,
        IComparer<SortRecord<T>> comparer, CancellationToken cancellationToken)
    {
        chunk.Sort(comparer);
        await using var writer = new StreamWriter(path, append: false, new UTF8Encoding(false));
        foreach (var row in chunk)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(JsonSerializer.Serialize(row).AsMemory(), cancellationToken);
        }
    }

    private static async Task MergeAsync<T>(string directory, int pass, int start, int count, string output,
        IComparer<SortRecord<T>> comparer, CancellationToken cancellationToken)
    {
        var readers = new List<StreamReader>(count);
        var queue = new PriorityQueue<int, SortRecord<T>>(comparer);
        try
        {
            for (var index = 0; index < count; index++)
            {
                var reader = new StreamReader(RunPath(directory, pass, start + index), Encoding.UTF8);
                readers.Add(reader);
                if (await reader.ReadLineAsync(cancellationToken) is { } line)
                    queue.Enqueue(index, JsonSerializer.Deserialize<SortRecord<T>>(line)!);
            }
            await using var writer = new StreamWriter(output, append: false, new UTF8Encoding(false));
            while (queue.TryDequeue(out var index, out var row))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await writer.WriteLineAsync(JsonSerializer.Serialize(row).AsMemory(), cancellationToken);
                if (await readers[index].ReadLineAsync(cancellationToken) is { } line)
                    queue.Enqueue(index, JsonSerializer.Deserialize<SortRecord<T>>(line)!);
            }
        }
        finally
        {
            foreach (var reader in readers) reader.Dispose();
        }
    }

    private sealed record SortRecord<T>(long Sequence, T Value);
}
