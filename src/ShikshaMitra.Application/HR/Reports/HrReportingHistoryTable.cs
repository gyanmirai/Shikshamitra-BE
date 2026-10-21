using System.Collections.Generic;

namespace ShikshaMitra.HR.Reports;

public static class HrReportingHistoryTable
{
	public static List<HrReportFieldDto> Columns => [
		new() { Id = "name", Type = "text" }, new() { Id = "status", Type = "text" },
		new() { Id = "period", Type = "text" }, new() { Id = "capturedAt", Type = "datetime" },
		new() { Id = "rowCount", Type = "number" } ];
	public static Dictionary<string, object?> Values(HrReportRunDto run) => new() {
		["name"] = run.Name, ["status"] = run.IsExpired ? "Expired" : run.Status,
		["period"] = run.Parameters.FromDate.HasValue ? $"{run.Parameters.FromDate:yyyy-MM-dd} to {run.Parameters.ToDate:yyyy-MM-dd}" : "Current records",
		["capturedAt"] = run.CapturedAt.ToString("O"), ["rowCount"] = run.RowCount };
}
