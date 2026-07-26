using System;

namespace ShikshaMitra.HR.Reports;

internal static class HrReportExportErrorFormatter
{
	private const string PdfMessage =
		"PDF export could not be generated for the selected report. Reduce the number of columns or export to Excel, then try again.";
	private const string ExcelMessage =
		"Excel export could not be generated for the selected report. Review the filters and try again.";

	public static string ToPersistedMessage(string? format) =>
		string.Equals(format, HrReportFormat.Pdf, StringComparison.OrdinalIgnoreCase)
			? PdfMessage
			: ExcelMessage;

	public static string NormalizePersistedMessage(string? message, string? format)
	{
		if (!string.IsNullOrWhiteSpace(message) &&
			(message.StartsWith("PDF export could not be generated", StringComparison.Ordinal) ||
			 message.StartsWith("Excel export could not be generated", StringComparison.Ordinal)))
		{
			return message;
		}

		return ToPersistedMessage(format);
	}
}
