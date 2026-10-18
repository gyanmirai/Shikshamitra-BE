using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ShikshaMitra.HR.Reports;

public static class HrReportingDocumentWriter
{
	public static byte[] Excel(HrReportRun run, HrReportingResult result)
	{
		var parameters = JsonSerializer.Deserialize<HrReportParametersDto>(run.ParametersJson, HrReportingEngine.Json)!;
		using var workbook = new XLWorkbook(); var sheet = workbook.Worksheets.Add("Report");
		sheet.Cell(1, 1).Value = result.SchoolName;
		sheet.Cell(2, 1).Value = run.Name;
		sheet.Cell(3, 1).Value = Context(run);
		const int header = 5;
		for (var col = 0; col < result.Columns.Count; col++) sheet.Cell(header, col + 1).Value = result.Columns[col].Label;
		var index = header + 1;
		foreach (var row in result.Rows)
		{
			for (var col = 0; col < result.Columns.Count; col++)
			{
				var field = result.Columns[col]; var cell = sheet.Cell(index, col + 1); var value = Scalar(row.GetValueOrDefault(field.Id));
				if (value == null) { cell.Value = "Unavailable"; continue; }
				if (field.Type is "number" or "money" && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
				{ cell.Value = number; cell.Style.NumberFormat.Format = field.Type == "money" ? "#,##0.00" : "0.##"; }
				else if (field.Type == "date" && DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
				{ cell.Value = date.ToDateTime(TimeOnly.MinValue); cell.Style.DateFormat.Format = "dd-mmm-yyyy"; }
				else if (field.Type == "datetime" && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var instant))
				{ cell.Value = TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(parameters.TimeZone)).DateTime; cell.Style.DateFormat.Format = "dd-mmm-yyyy hh:mm"; }
				else cell.Value = value;
			}
			index++;
		}
		if (result.Columns.Count > 0)
		{
			var range = sheet.Range(header, 1, Math.Max(header, index - 1), result.Columns.Count);
			range.SetAutoFilter(); sheet.Row(header).Style.Font.Bold = true; sheet.Row(header).Style.Fill.BackgroundColor = XLColor.LightGray;
			for (var col = 0; col < result.Columns.Count; col++) if (result.Totals.TryGetValue(result.Columns[col].Id, out var total) && total.HasValue) sheet.Cell(index + 1, col + 1).Value = total.Value;
			var sampleLastRow = Math.Min(index, 200);
			sheet.Columns(1, result.Columns.Count).AdjustToContents(header, sampleLastRow);
			foreach (var column in sheet.ColumnsUsed()) column.Width = Math.Clamp(column.Width, 12, 40);
		}
		sheet.SheetView.FreezeRows(header); sheet.SheetView.FreezeColumns(Math.Min(2, result.Columns.Count));
		var info = workbook.Worksheets.Add("Report details");
		var lines = new[] { result.SchoolName, result.SchoolAddress, run.Name, Context(run), "Template: " + run.TemplateId + " revision " + run.Revision, "Rules: " + Rules(run), "Parameters: " + run.ParametersJson, "Rows: " + run.RowCount };
		for (var i = 0; i < lines.Length; i++) info.Cell(i + 1, 1).Value = lines[i];
		for (var i = 0; i < result.Warnings.Count; i++) info.Cell(lines.Length + i + 1, 1).Value = result.Warnings[i];
		info.Column(1).Width = 100;
		using var stream = new MemoryStream(); workbook.SaveAs(stream); return stream.ToArray();
	}
	public static byte[] Pdf(HrReportRun run, HrReportingResult result)
	{
		if (result.Rows.Count > 10000 || (long)result.Rows.Count * result.Columns.Count > 200000) throw HrReportingEngine.Invalid("PDF supports up to 10,000 rows and 200,000 cells. Use Excel or narrow the report.");
		QuestPDF.Settings.License = LicenseType.Community;
		var design = JsonSerializer.Deserialize<HrReportDesignDto>(run.DefinitionJson, HrReportingEngine.Json)!;
		var parameters = JsonSerializer.Deserialize<HrReportParametersDto>(run.ParametersJson, HrReportingEngine.Json)!;
		var firstColumns = result.Columns.Take(2).ToList();
		var sections = result.Columns.Count <= 10 ? new List<List<HrReportFieldDto>> { result.Columns }
			: result.Columns.Skip(2).Chunk(8).Select(chunk => firstColumns.Concat(chunk).ToList()).ToList();
		return Document.Create(document =>
		{
			foreach (var fields in sections)
			{
				document.Page(page =>
				{
					var size = design.PaperSize == "A3" ? PageSizes.A3 : PageSizes.A4;
					page.Size(design.Orientation == "portrait" ? size.Portrait() : size.Landscape()); page.Margin(24);
					page.DefaultTextStyle(s => s.FontSize(8));
					page.Header().PaddingBottom(12).Column(column =>
					{
						column.Item().Text(result.SchoolName).FontSize(14).SemiBold();
						if (!string.IsNullOrEmpty(result.SchoolAddress)) column.Item().Text(result.SchoolAddress);
						column.Item().Text(run.Name).FontSize(12).SemiBold();
						column.Item().Text(Context(run));
						column.Item().Text("Staff selection: " + (parameters.EmployeeIds.Count > 0 ? parameters.EmployeeIds.Count + " selected staff" : "All authorized staff") + "; departments: " + (parameters.Departments.Count > 0 ? string.Join(", ", parameters.Departments) : "All"));
						column.Item().Text("Rules: " + Rules(run));
						if (parameters.IncludeDrafts) column.Item().Text("Includes saved draft payroll. Draft rows are labelled.");
					});
					page.Content().Table(table =>
					{
						table.ColumnsDefinition(columns => { foreach (var _ in fields) columns.RelativeColumn(); });
						table.Header(header => { foreach (var f in fields) header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(f.Label).SemiBold(); });
						foreach (var row in result.Rows)
							foreach (var f in fields) table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(Display(row.GetValueOrDefault(f.Id), f.Type, parameters.TimeZone));
						if (result.Rows.Count == 0) foreach (var _ in fields) table.Cell().Padding(6).Text("No matching records");
						foreach (var f in fields) table.Cell().Padding(5).Text(result.Totals.TryGetValue(f.Id, out var n) && n.HasValue ? n.Value.ToString("N2", CultureInfo.InvariantCulture) : "").SemiBold();
					});
					page.Footer().AlignRight().Text(text => { text.Span("Page "); text.CurrentPageNumber(); text.Span(" of "); text.TotalPages(); });
				});
			}
		}).GeneratePdf();
	}
	private static string Context(HrReportRun run)
	{
		var p = JsonSerializer.Deserialize<HrReportParametersDto>(run.ParametersJson, HrReportingEngine.Json)!;
		var period = p.FromDate.HasValue ? $"{p.FromDate:dd MMM yyyy} to {p.ToDate:dd MMM yyyy}" : "Current records";
		return $"Period: {period} | Captured: {TimeZoneInfo.ConvertTime(run.CapturedAt, TimeZoneInfo.FindSystemTimeZoneById(p.TimeZone)):dd MMM yyyy HH:mm} {p.TimeZone} | {run.RowCount} rows";
	}
	private static string Rules(HrReportRun run)
	{
		var design = JsonSerializer.Deserialize<HrReportDesignDto>(run.DefinitionJson, HrReportingEngine.Json)!;
		var p = JsonSerializer.Deserialize<HrReportParametersDto>(run.ParametersJson, HrReportingEngine.Json)!;
		var fields = HrReportingCatalogue.Datasets().Single(x => x.Id == design.DataSource).Fields.ToDictionary(x => x.Id, x => x.Label);
		string Group(HrReportConditionGroupDto g, string path)
		{
			var conditions = g.Conditions.Select((c, i) =>
			{
				var supplied = c.AskWhenGenerating ? p.FilterValues.GetValueOrDefault(path + "." + i) : null;
				var value = supplied?.Value ?? c.Value; var values = supplied?.Values ?? c.Values;
				var comparison = c.Operator switch { "equals" => "is", "notEquals" => "is not", "gte" => "at least", "lte" => "at most", "gt" => "greater than", "lt" => "less than", "in" => "in", "notIn" => "not in", "empty" => "is empty", "notEmpty" => "is not empty", _ => c.Operator };
				return fields.GetValueOrDefault(c.Field, c.Field) + " " + comparison + " " + (values?.Count > 0 ? string.Join(", ", values) : value ?? "");
			}).Concat(g.Groups.Select((child, i) => "(" + Group(child, path + ".g" + i) + ")"));
			return string.Join(g.Match == "any" ? " OR " : " AND ", conditions);
		}
		var rules = Group(design.Filters, "filters"); return string.IsNullOrEmpty(rules) ? "No additional template filters" : rules;
	}
	private static string? Scalar(object? value) => value is JsonElement element ? element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : element.ToString() : Convert.ToString(value, CultureInfo.InvariantCulture);
	private static string Display(object? value, string type, string timezone)
	{
		var text = Scalar(value); if (text == null) return "Unavailable";
		if (type == "money" && decimal.TryParse(text, CultureInfo.InvariantCulture, out var money)) return money.ToString("N2", CultureInfo.InvariantCulture);
		if (type == "datetime" && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp)) return TimeZoneInfo.ConvertTime(timestamp, TimeZoneInfo.FindSystemTimeZoneById(timezone)).ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);
		return text;
	}
}
