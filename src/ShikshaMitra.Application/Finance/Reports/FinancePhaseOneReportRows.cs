using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ShikshaMitra.Finance.Reports;

internal sealed class FinancePhaseOneReportRows(FinanceReportPreviewDto payload)
{
    internal static readonly TimeSpan SchoolOffset = TimeSpan.FromMinutes(330);

    private readonly List<Dictionary<string, string?>> rows = [];

    public void Define(params (string Id, string Label, string? Kind)[] columns) =>
        payload.Columns = columns.Select(c => new FinanceReportColumnDto
            { Id = c.Id, Label = c.Label, Kind = c.Kind }).ToList();

    public void Add(params object?[] values)
    {
        var row = new Dictionary<string, string?>(payload.Columns.Count);
        for (var index = 0; index < payload.Columns.Count; index++)
        {
            row[payload.Columns[index].Id] = values[index] switch
            {
                DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DateTimeOffset instant => instant.ToOffset(SchoolOffset).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                IFormattable value => value.ToString(null, CultureInfo.InvariantCulture),
                object value => value.ToString(),
                _ => null
            };
        }
        rows.Add(row);
    }

    public FinanceReportPreviewDto Finish(FinanceReportRequestDto request, bool exportAllRows)
    {
        payload.TotalCount = rows.Count;
        payload.Page = exportAllRows ? 1 : request.Page;
        payload.PageSize = exportAllRows ? rows.Count : request.PageSize;
        var offset = (long)(request.Page - 1) * request.PageSize;
        payload.Rows = exportAllRows ? rows : offset >= rows.Count ? [] :
            rows.Skip((int)offset).Take(request.PageSize).ToList();
        return payload;
    }

    public static bool Matches(string? text, string? filter) =>
        string.IsNullOrWhiteSpace(filter) || (text?.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase) ?? false);

    public static string AgeingBucket(int days) => days switch
    {
        <= 0 => "Not overdue",
        <= 30 => "1–30 days",
        <= 60 => "31–60 days",
        <= 90 => "61–90 days",
        _ => "91+ days"
    };
}
