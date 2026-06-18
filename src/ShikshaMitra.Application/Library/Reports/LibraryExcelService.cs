using System.Linq;
using ShikshaMitra.Common;
using ShikshaMitra.Common.Excel;
using ShikshaMitra.Common.Excel.Elements;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Library.Reports;

public class LibraryExcelService(IExcelService excelService) : ITransientDependency
{
	public byte[] GenerateReportExcel(LibraryReportPreviewDto report)
	{
		var sheet = new ExcelSheet
		{
			SheetName = SanitizeSheetName(report.ReportType),
			Rows =
			[
				new ExcelRow
				{
					Cells = report.Columns
						.Select(column => new ExcelCell { Value = column.Label })
						.ToList()
				}
			]
		};

		sheet.Rows.AddRange(report.Rows.Select(row => new ExcelRow
		{
			Cells = report.Columns
				.Select(column => new ExcelCell
				{
					Value = row.TryGetValue(column.Id, out var value) ? value ?? string.Empty : string.Empty
				})
				.ToList()
		}));

		return excelService.GenerateExcelAsBytes(new ExcelDocument
		{
			Sheets = [sheet],
			OutputFileName = $"{report.ReportType}.xlsx"
		});
	}

	private static string SanitizeSheetName(string name)
	{
		var invalidChars = new[] { ':', '\\', '/', '?', '*', '[', ']' };
		var sanitized = invalidChars.Aggregate(name, (current, ch) => current.Replace(ch, '-'));
		return string.IsNullOrWhiteSpace(sanitized)
			? "Library Report"
			: sanitized.Length <= 31 ? sanitized : sanitized[..31];
	}
}
