using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ShikshaMitra.HR.Reports;

public record HrOperationalTableRow(JsonElement Export, JsonElement Raw, Dictionary<string, object?> Values);

// Query/export share the same authorized population and server-owned projection. No client type,
// expression, property path or permission can choose an additional data source or hidden field.
public static class HrOperationalTableQuery
{
	public static List<JsonElement> Items(JsonElement envelope)
	{
		foreach (var key in new[] { "items", "employees", "rows" })
			if (envelope.TryGetProperty(key, out var items) && items.ValueKind == JsonValueKind.Array)
				return items.EnumerateArray().Select(row => row.Clone()).ToList();
		throw HrReportingEngine.Invalid("This table source did not return a complete page contract.");
	}
	public static JsonElement Envelope(string table, JsonElement original, List<HrOperationalTableRow> selected,
		List<HrOperationalTableRow> matching, int offset, int size, List<HrReportFieldDto> columns)
	{
		var envelope = JsonNode.Parse(original.GetRawText())!.AsObject();
		var key = envelope.ContainsKey("items") ? "items" : envelope.ContainsKey("employees") ? "employees" : "rows";
		envelope[key] = JsonSerializer.SerializeToNode(selected.Select(row => row.Raw));
		envelope["totalCount"] = matching.Count;
		if (envelope.ContainsKey("page")) envelope["page"] = table is "payroll-monthly-workbench-table" or "esi-employee-impact" ? offset / size : offset / size + 1;
		if (envelope.ContainsKey("currentPage")) envelope["currentPage"] = offset / size + 1;
		if (envelope.ContainsKey("totalPages")) envelope["totalPages"] = Math.Max(1, (int)Math.Ceiling((double)matching.Count / size));
		if (envelope.ContainsKey("pageSize")) envelope["pageSize"] = size;
		if (envelope.ContainsKey("hasMore")) envelope["hasMore"] = offset + size < matching.Count;
		if (table == "hr-salary-documents")
		{
			// Salary previews label these as displayed-row totals. Unavailable values stay absent.
			var totals = new Dictionary<string, decimal>();
			foreach (var id in columns.Where(column => column.Type is "number" or "money").Select(column => column.Id))
			{
				var values = selected.Select(row => row.Values.GetValueOrDefault(id)).Select(value =>
					decimal.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), System.Globalization.NumberStyles.Number,
						System.Globalization.CultureInfo.InvariantCulture, out var number) ? (decimal?)number : null).Where(value => value.HasValue).ToList();
				if (values.Count > 0) totals[id] = values.Sum()!.Value;
			}
			envelope["totals"] = JsonSerializer.SerializeToNode(totals);
		}
		return JsonSerializer.SerializeToElement(envelope);
	}
}
