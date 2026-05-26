using ShikshaMitra.Common;
using ShikshaMitra.Common.Excel;
using ShikshaMitra.Common.Excel.Elements;
using System.Linq;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Academic.Reports;

public class AcademicExcelService(IExcelService excelService) : ITransientDependency
{
    public byte[] GenerateReportExcel(AcademicReportPreviewDto report)
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

        var document = new ExcelDocument { Sheets = [sheet], OutputFileName = "AcademicReport.xlsx" };

        return excelService.GenerateExcelAsBytes(document);
    }

    private static string SanitizeSheetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Sheet1";
        var invalidChars = new[] { '\\', '/', '?', '*', '[', ']' };
        var safe = new string(name.Where(c => !invalidChars.Contains(c)).ToArray());
        return safe.Length > 31 ? safe[..31] : safe;
    }
}
