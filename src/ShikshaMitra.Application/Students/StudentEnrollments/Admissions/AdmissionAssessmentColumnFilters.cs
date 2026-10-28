using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionAssessmentColumnFilters
{
	public static IEnumerable<AdmissionAssessmentColumnFilterRow> Apply(IEnumerable<AdmissionAssessmentColumnFilterRow> rows, string? filters)
	{
		if (filters?.Length > 12000) throw Invalid();
		if (string.IsNullOrWhiteSpace(filters)) return rows;
		try
		{
			using var document = JsonDocument.Parse(filters);
			if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() > 20) throw Invalid();
			foreach (var filter in document.RootElement.EnumerateArray())
			{
				if (filter.ValueKind != JsonValueKind.Object || !filter.TryGetProperty("value", out var raw) || raw.ValueKind != JsonValueKind.String) throw Invalid();
				var column = Text(filter, "column");
				var operation = Text(filter, "type");
				var valueType = Text(filter, "valueType");
				var value = raw.GetString()!;
				Func<AdmissionAssessmentColumnFilterRow, string> field = column switch
				{
					"applicationNumber" => row => row.ApplicationNumber,
					"className" => row => row.ClassName,
					"sessionName" => row => row.SessionName,
					"attendance" => row => row.Attendance,
					"result" => row => row.Outcome,
					"status" => row => row.Status,
					_ => throw Invalid()
				};
				if (column is "attendance" or "result" or "status")
				{
					var valid = column switch
					{
						"attendance" => value is "pending" or "present" or "absent" or "notMarked" or "notRequired" or "notApplicable",
						"status" => value is "notBooked" or "scheduled" or "attendancePending" or "resultPending" or "reviewPending" or "ongoing" or "passed" or "failed" or "absent" or "waived" or "retakeRequired",
						_ => value is "passed" or "failed" or "waived"
					};
					if (valueType != "enum" || operation != "equals" || !valid) throw Invalid();
					rows = rows.Where(row => field(row) == value);
				}
				else
				{
					if (valueType != "string" || string.IsNullOrWhiteSpace(value) || value.Length > 128 || operation is not ("contains" or "equals" or "startsWith" or "endsWith")) throw Invalid();
					rows = rows.Where(row => operation switch
					{
						"equals" => string.Equals(field(row), value, StringComparison.OrdinalIgnoreCase),
						"contains" => field(row).Contains(value, StringComparison.OrdinalIgnoreCase),
						"startsWith" => field(row).StartsWith(value, StringComparison.OrdinalIgnoreCase),
						_ => field(row).EndsWith(value, StringComparison.OrdinalIgnoreCase)
					});
				}
			}
			return rows;
		}
		catch (JsonException) { throw Invalid(); }
	}

	private static string Text(JsonElement filter, string name) => filter.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : throw Invalid();
	private static BusinessException Invalid() => new(AdmissionAssessmentErrorCodes.InvalidState, "Choose valid assessment column filters.");
}

public class AdmissionAssessmentColumnFilterRow
{
	public Guid StageId { get; set; }
	public string ApplicationNumber { get; set; } = string.Empty;
	public string ClassName { get; set; } = string.Empty;
	public string SessionName { get; set; } = string.Empty;
	public string Attendance { get; set; } = "pending";
	public string Outcome { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
}
