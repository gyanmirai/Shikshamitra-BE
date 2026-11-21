using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Finance.Reports;

public class FinanceExcelService : ITransientDependency
{
    public Task<byte[]> GenerateReportExcelAsync(FinanceReportPreviewDto data)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Finance report");
        for (var index = 0; index < data.Columns.Count; index++)
        {
            var column = data.Columns[index];
            sheet.Cell(1, index + 1).Value = column.Label;
            sheet.Column(index + 1).Width = Math.Clamp(column.Label.Length + 3, 15, 36);
            if (column.Kind == "money")
                sheet.Column(index + 1).Style.NumberFormat.Format = "#,##0.00";
        }
        sheet.Row(1).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);
        for (var rowIndex = 0; rowIndex < data.Rows.Count; rowIndex++)
        {
            var row = data.Rows[rowIndex];
            for (var index = 0; index < data.Columns.Count; index++)
            {
                var column = data.Columns[index];
                if (!row.TryGetValue(column.Id, out var value) || value == null)
                    continue;
                var cell = sheet.Cell(rowIndex + 2, index + 1);
                if (column.Kind is "money" or "number" &&
                    decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                    cell.Value = (double)amount;
                else
                    cell.Value = value;
            }
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Task.FromResult(stream.ToArray());
    }
}
