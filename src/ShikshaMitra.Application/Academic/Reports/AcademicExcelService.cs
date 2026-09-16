using ShikshaMitra.Common;
using ShikshaMitra.Common.Excel;
using ShikshaMitra.Common.Excel.Elements;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Academic.Reports;

public class AcademicExcelService(IExcelService excelService) : ITransientDependency
{
    public async Task<int> WriteReportExcelAsync(
        AcademicReportPreviewDto report,
        IAsyncEnumerable<Dictionary<string, string?>> rows,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        WritePackagePart(archive, "[Content_Types].xml", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml"/></Types>
            """);
        WritePackagePart(archive, "_rels/.rels", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
            """);
        WritePackagePart(archive, "xl/_rels/workbook.xml.rels", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml"/></Relationships>
            """);
        const string spreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        using (var stream = archive.CreateEntry("xl/workbook.xml", CompressionLevel.Fastest).Open())
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false) }))
        {
            writer.WriteStartElement("workbook", spreadsheetNamespace);
            writer.WriteStartElement("sheets", spreadsheetNamespace);
            writer.WriteStartElement("sheet", spreadsheetNamespace);
            writer.WriteAttributeString("name", SanitizeSheetName(report.ReportType));
            writer.WriteAttributeString("sheetId", "1");
            writer.WriteAttributeString("r", "id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships", "rId1");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        // Spool sequential shared strings to disk without a workbook-sized deduplication dictionary.
        // Shared-string encoding is understood consistently by Excel and ClosedXML, including _xHHHH_ literals.
        await using var strings = AcademicReportExportRows.CreateTemporaryFile();
        using var stringWriter = XmlWriter.Create(strings, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false), Async = true, NewLineHandling = NewLineHandling.Entitize
        });
        stringWriter.WriteStartElement("sst", spreadsheetNamespace);
        var stringIndex = 0;
        var rowCount = 0;
        using (var sheetStream = archive.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Fastest).Open())
        {
        using var sheetWriter = XmlWriter.Create(sheetStream, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false), Async = true, NewLineHandling = NewLineHandling.Entitize
        });
        sheetWriter.WriteStartElement("worksheet", spreadsheetNamespace);
        sheetWriter.WriteStartElement("sheetData", spreadsheetNamespace);
        WriteRow(sheetWriter, stringWriter, ref stringIndex, report.Columns.Select(column => column.Label), spreadsheetNamespace);
        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++rowCount >= 1_048_576)
                throw new ArgumentOutOfRangeException(nameof(rows), "An Excel worksheet supports at most 1,048,576 rows including the header.");
            WriteRow(sheetWriter, stringWriter, ref stringIndex, report.Columns.Select(column => row.GetValueOrDefault(column.Id) ?? string.Empty), spreadsheetNamespace);
            if (rowCount % 256 == 0)
            {
                await sheetWriter.FlushAsync();
                await stringWriter.FlushAsync();
            }
        }
        sheetWriter.WriteEndElement();
        sheetWriter.WriteEndElement();
        await sheetWriter.FlushAsync();
        }
        stringWriter.WriteEndElement();
        await stringWriter.FlushAsync();
        strings.Position = 0;
        using (var sharedStrings = archive.CreateEntry("xl/sharedStrings.xml", CompressionLevel.Fastest).Open())
            await strings.CopyToAsync(sharedStrings, cancellationToken);
        return rowCount;
    }

    private static void WriteRow(XmlWriter writer, XmlWriter strings, ref int stringIndex,
        IEnumerable<string> values, string spreadsheetNamespace)
    {
        writer.WriteStartElement("row", spreadsheetNamespace);
        foreach (var value in values)
        {
            if (value.Length > 32_767)
                throw new ArgumentOutOfRangeException(nameof(values), "An Excel cell supports at most 32,767 characters.");
            writer.WriteStartElement("c", spreadsheetNamespace);
            writer.WriteAttributeString("t", "s");
            writer.WriteElementString("v", spreadsheetNamespace, (stringIndex++).ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteEndElement();
            strings.WriteStartElement("si", spreadsheetNamespace);
            strings.WriteStartElement("t", spreadsheetNamespace);
            strings.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
            // SpreadsheetML escapes control characters and literal strings resembling its escape sequence.
            var escaped = Regex.Replace(value, "_x[0-9A-Fa-f]{4}_", match => "_x005F_" + match.Value[1..]);
            escaped = Regex.Replace(escaped, "[\\x00-\\x08\\x0B\\x0C\\x0E-\\x1F]", match => $"_x{(int)match.Value[0]:X4}_");
            strings.WriteString(escaped);
            strings.WriteEndElement();
            strings.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static void WritePackagePart(ZipArchive archive, string path, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(path, CompressionLevel.Fastest).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

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
