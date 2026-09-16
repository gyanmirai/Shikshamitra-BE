using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using ShikshaMitra.Common.Excel;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Academic.Reports;

public class AcademicReportStreaming_Tests
{
    [Fact]
    public async Task Streamed_Excel_And_Csv_Should_Preserve_Cell_Values_Order_And_Csv_Bytes()
    {
        var report = new AcademicReportPreviewDto
        {
            ReportType = "Report/with?characters",
            Columns = [new() { Id = "Number", Label = "Number" }, new() { Id = "Value", Label = "Value" }],
            Rows = Enumerable.Range(0, 650).Select(index => new Dictionary<string, string?>
            {
                ["Number"] = index.ToString(),
                ["Value"] = (index % 5) switch
                {
                    0 => " spaces ", 1 => "quotes \" and, commas", 2 => "first\r\nsecond",
                    3 => "Unicode मराठी _x0001_", _ => null
                }
            }).ToList()
        };
        var source = AcademicReportExportRows.FromReport(report);
        using var excel = new MemoryStream();
        var excelService = new AcademicExcelService(new ClosedXmlExcelService());
        (await excelService.WriteReportExcelAsync(report, source.Rows, excel)).ShouldBe(650);
        excel.CanWrite.ShouldBeTrue();
        excel.Position = 0;
        using (var workbook = new XLWorkbook(excel))
        {
            var worksheet = workbook.Worksheets.Single();
            worksheet.Name.ShouldBe("Reportwithcharacters");
            worksheet.Cell(1, 1).GetString().ShouldBe("Number");
            for (var index = 0; index < report.Rows.Count; index++)
            {
                worksheet.Cell(index + 2, 1).GetString().ShouldBe(report.Rows[index]["Number"]);
                worksheet.Cell(index + 2, 2).GetString().ShouldBe(report.Rows[index]["Value"] ?? string.Empty);
            }
        }
        using var csv = new MemoryStream();
        var csvService = new AcademicCsvService();
        (await csvService.WriteReportCsvAsync(report, source.Rows, csv)).ShouldBe(650);
        csv.ToArray().ShouldBe(csvService.GenerateReportCsv(report));
        csv.CanWrite.ShouldBeTrue();
    }

    [Fact]
    public async Task External_Sort_Should_Preserve_Stable_Order_Across_Multiple_Merge_Passes()
    {
        var expected = Enumerable.Range(0, 73).Select(index => new SortItem(72 - index, index % 4)).ToList();
        var actual = new List<SortItem>();
        await foreach (var item in AcademicReportExternalSort.SortAsync(Enumerate(expected),
            (left, right) => left.Key.CompareTo(right.Key), chunkSize: 2)) actual.Add(item);
        actual.ShouldBe(expected.OrderBy(item => item.Key));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task External_Sort_Should_Delete_Spilled_Rows_On_Failure_Or_Cancellation(bool cancel)
    {
        var root = Path.Combine(Path.GetTempPath(), $"academic-sort-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        using var cancellation = new CancellationTokenSource();
        try
        {
            async Task RunAsync()
            {
                await foreach (var _ in AcademicReportExternalSort.SortAsync(
                    FailingRows(cancel, cancellation), (left, right) => left.CompareTo(right), chunkSize: 2,
                    cancellationToken: cancellation.Token, temporaryDirectoryRoot: root)) { }
            }
            if (cancel) await Should.ThrowAsync<OperationCanceledException>(RunAsync);
            else await Should.ThrowAsync<InvalidOperationException>(RunAsync);
            Directory.EnumerateFileSystemEntries(root).ShouldBeEmpty();
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Partial_Enumeration_Should_Delete_Sort_Files_And_Output_File()
    {
        var root = Path.Combine(Path.GetTempPath(), $"academic-sort-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            await foreach (var _ in AcademicReportExternalSort.SortAsync(Enumerate(Enumerable.Range(0, 40)),
                (left, right) => right.CompareTo(left), chunkSize: 2, temporaryDirectoryRoot: root)) break;
            Directory.EnumerateFileSystemEntries(root).ShouldBeEmpty();
            string path;
            await using (var file = AcademicReportExportRows.CreateTemporaryFile())
            {
                path = file.Name;
                await file.WriteAsync(new byte[] { 1, 2, 3 });
            }
            File.Exists(path).ShouldBeFalse();
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static async IAsyncEnumerable<T> Enumerate<T>(IEnumerable<T> items,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<int> FailingRows(bool cancel, CancellationTokenSource cancellation)
    {
        for (var index = 0; index < 40; index++)
        {
            if (index == 35)
            {
                if (cancel)
                {
                    cancellation.Cancel();
                    cancellation.Token.ThrowIfCancellationRequested();
                }
                throw new InvalidOperationException("Source failed after spilling rows.");
            }
            yield return index;
        }
        await Task.CompletedTask;
    }

    private sealed record SortItem(int Id, int Key);
}
