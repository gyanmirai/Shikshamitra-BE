using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Academic.Reports;

public class AcademicCsvService : ITransientDependency
{
	public async Task<int> WriteReportCsvAsync(AcademicReportPreviewDto report,
		IAsyncEnumerable<Dictionary<string, string?>> rows, Stream destination, CancellationToken cancellationToken = default)
	{
		// GetBytes in the existing API emits no BOM; preserve those exact CSV bytes and no trailing newline.
		using var writer = new StreamWriter(destination, new UTF8Encoding(false), 16 * 1024, leaveOpen: true);
		await writer.WriteAsync(string.Join(',', report.Columns.Select(column => Escape(column.Label))).AsMemory(), cancellationToken);
		var count = 0;
		await foreach (var row in rows.WithCancellation(cancellationToken))
		{
			await writer.WriteAsync("\r\n".AsMemory(), cancellationToken);
			await writer.WriteAsync(string.Join(',', report.Columns.Select(column => Escape(row.GetValueOrDefault(column.Id)))).AsMemory(), cancellationToken);
			count++;
		}
		await writer.FlushAsync(cancellationToken);
		return count;
	}

	public byte[] GenerateReportCsv(AcademicReportPreviewDto report)
	{
		var lines = new List<string>
		{
			string.Join(',', report.Columns.Select(column => Escape(column.Label)))
		};
		lines.AddRange(report.Rows.Select(row => string.Join(',', report.Columns.Select(column =>
			Escape(row.GetValueOrDefault(column.Id))))));

		return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)
			.GetBytes(string.Join("\r\n", lines));
	}

	private static string Escape(string? value)
	{
		var text = value ?? string.Empty;
		if (!text.Contains(',') && !text.Contains('"') && !text.Contains('\r') && !text.Contains('\n'))
		{
			return text;
		}
		return $"\"{text.Replace("\"", "\"\"")}\"";
	}
}
