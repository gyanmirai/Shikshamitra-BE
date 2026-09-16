using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Reports;

/// <summary>Replays report rows for synchronous PDF composition without buffering the source dataset.</summary>
internal sealed class AcademicReportRowSpool : IAsyncDisposable
{
	private readonly FileStream _file = AcademicReportExportRows.CreateTemporaryFile();
	private readonly CancellationToken _cancellationToken;
	public int Count { get; private set; }

	private AcademicReportRowSpool(CancellationToken cancellationToken) => _cancellationToken = cancellationToken;

	public static async Task<AcademicReportRowSpool> CreateAsync(
		IAsyncEnumerable<Dictionary<string, string?>> source, CancellationToken cancellationToken)
	{
		var spool = new AcademicReportRowSpool(cancellationToken);
		try
		{
			await using var writer = new StreamWriter(spool._file, new UTF8Encoding(false), 64 * 1024, leaveOpen: true);
			await foreach (var row in source.WithCancellation(cancellationToken))
			{
				cancellationToken.ThrowIfCancellationRequested();
				await writer.WriteLineAsync(JsonSerializer.Serialize(row).AsMemory(), cancellationToken);
				spool.Count = checked(spool.Count + 1);
			}
			await writer.FlushAsync(cancellationToken);
			return spool;
		}
		catch
		{
			await spool.DisposeAsync();
			throw;
		}
	}

	public IEnumerable<Dictionary<string, string?>> ReadRows()
	{
		_file.Position = 0;
		using var reader = new StreamReader(_file, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, 64 * 1024, leaveOpen: true);
		while (reader.ReadLine() is { } line)
		{
			_cancellationToken.ThrowIfCancellationRequested();
			yield return JsonSerializer.Deserialize<Dictionary<string, string?>>(line)!;
		}
	}

	public ValueTask DisposeAsync() => _file.DisposeAsync();
}
