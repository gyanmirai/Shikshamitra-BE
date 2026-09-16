using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Reports;

internal sealed record AcademicReportExportRows(
    AcademicReportPreviewDto Metadata,
    IAsyncEnumerable<Dictionary<string, string?>> Rows)
{
    public static AcademicReportExportRows FromReport(AcademicReportPreviewDto report) => new(report, Enumerate(report.Rows));

    private static async IAsyncEnumerable<Dictionary<string, string?>> Enumerate(
        IEnumerable<Dictionary<string, string?>> rows,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
        }
        await Task.CompletedTask;
    }

    public static FileStream CreateTemporaryFile()
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew, Access = FileAccess.ReadWrite, Share = FileShare.None, BufferSize = 64 * 1024,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose
        };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return new FileStream(Path.Combine(Path.GetTempPath(), $"shikshamitra-academic-{Guid.NewGuid():N}.tmp"), options);
    }
}
