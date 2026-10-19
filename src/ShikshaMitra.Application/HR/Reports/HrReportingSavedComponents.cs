using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.HR.Payroll.SalarySlips;

namespace ShikshaMitra.HR.Reports;

public record HrReportingSavedComponent(string Key, string Label, decimal Amount, bool HasIdentity);

public static class HrReportingSavedComponents
{
	public static string Serialize(SalarySlipDto? facts, string? earnings, string? deductions)
	{
		var result = new List<HrReportingSavedComponent>();
		if (facts != null)
		{
			foreach (var (kind, lines) in new[] { ("Earning", facts.Earnings), ("Deduction", facts.Deductions) })
				result.AddRange(lines.Select(x => Line(kind, x.ComponentId, x.SourceLineId, x.SemanticType, x.Name, x.Amount)));
		}
		else
		{
			foreach (var (kind, json) in new[] { ("Earning", earnings), ("Deduction", deductions) })
			{
				if (json == null) continue;
				using var document = JsonDocument.Parse(json);
				foreach (var line in document.RootElement.EnumerateArray())
				{
					var p = line.EnumerateObject().ToDictionary(x => x.Name, x => x.Value, StringComparer.OrdinalIgnoreCase);
					var id = p.TryGetValue("componentId", out var value) && value.ValueKind == JsonValueKind.String && value.TryGetGuid(out var parsed) ? (Guid?) parsed : null;
					var sourceId = p.TryGetValue("sourceLineId", out var sourceValue) && sourceValue.ValueKind == JsonValueKind.String && sourceValue.TryGetGuid(out var sourceGuid) ? (Guid?)sourceGuid : null;
					var semantic = p.TryGetValue("semanticType", out var semanticValue) && semanticValue.ValueKind == JsonValueKind.String ? semanticValue.GetString() : null;
					result.Add(Line(kind, id, sourceId, semantic, p["name"].GetString() ?? "Unnamed component", p["amount"].GetDecimal()));
				}
			}
		}
		return JsonSerializer.Serialize(result, HrReportingEngine.Json);
	}
	private static HrReportingSavedComponent Line(string kind, Guid? id, Guid? sourceId, string? semantic, string name, decimal amount)
	{
		var semanticIdentity = !string.IsNullOrEmpty(semantic) && semantic is not ("Ordinary" or "ORDINARY" or "None" or "NONE") ? "semantic:" + semantic : null;
		var identity = id?.ToString("N") ?? semanticIdentity ?? (sourceId.HasValue ? "line:" + sourceId.Value.ToString("N") : null);
		return new(kind + ":" + (identity ?? "unidentified:" + name), kind + ": " + name, amount, identity != null);
	}
}
