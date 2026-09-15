using System.Collections.Generic;
using System.Linq;
using System.Text;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Academic.Reports;

public class AcademicCsvService : ITransientDependency
{
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
