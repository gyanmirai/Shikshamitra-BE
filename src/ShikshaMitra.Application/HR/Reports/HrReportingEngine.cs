using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.HR.Reports;

public class HrReportingResult
{
	public string SchoolName { get; set; } = "";
	public string SchoolAddress { get; set; } = "";
	public List<Guid> EmployeeIds { get; set; } = [];
	public List<Guid> AssignmentIds { get; set; } = [];
	public List<HrReportFieldDto> Columns { get; set; } = [];
	public List<Dictionary<string, object?>> Rows { get; set; } = [];
	public Dictionary<string, decimal?> Totals { get; set; } = [];
	public List<string> Warnings { get; set; } = [];
	public HrReportChartPreviewDto? Chart { get; set; }
}

public static class HrReportingEngine
{
	public const int MaximumPopulation = 500000;
	public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
	public static BusinessException Invalid(string message) => new BusinessException(HRErrorCodes.ReportDesignInvalid).WithData("errors", message);
	public static HrReportDatasetDto Validate(HrReportDesignDto d)
	{
		if (d == null || d.Columns == null || d.Filters == null || d.Sorts == null || d.Groups == null || d.Summaries == null || d.Parameters == null
			|| d.Columns.Any(x => x == null || x.Label == null || string.IsNullOrWhiteSpace(x.Field)) || d.Sorts.Any(x => x == null || string.IsNullOrWhiteSpace(x.Field))
			|| d.Summaries.Any(x => x == null || string.IsNullOrWhiteSpace(x.Field)) || d.Groups.Any(string.IsNullOrWhiteSpace) || d.Parameters.Any(string.IsNullOrWhiteSpace)) throw Invalid("Provide a complete report definition.");
		if (JsonSerializer.Serialize(d, Json).Length > 65536) throw Invalid("The template definition exceeds 64 KB. Reduce its filters or labels.");
		var dataset = HrReportingCatalogue.Datasets().SingleOrDefault(x => x.Id == d.DataSource) ?? throw Invalid("Choose a supported dataset.");
		if (d.Version != 2 || !dataset.Layouts.Contains(d.Layout)) throw Invalid("Choose a supported definition version and layout.");
		if (d.Columns.Count is < 1 or > 60 || d.Columns.Select(x => x.Field).Distinct().Count() != d.Columns.Count) throw Invalid("Choose 1 to 60 distinct columns.");
		var fields = dataset.Fields.ToDictionary(x => x.Id);
		foreach (var column in d.Columns)
			if (!fields.ContainsKey(column.Field) || column.Label.Length > 128 || !new[] { "auto", "text", "number", "money", "date", "datetime" }.Contains(column.Format)) throw Invalid("A selected column or format is unsupported.");
		foreach (var column in d.Columns)
			if (column.Format is not ("auto" or "text") && column.Format != fields[column.Field].Type && !(column.Format == "number" && fields[column.Field].Type == "money")) throw Invalid("Choose a display format supported by the column's data type.");
		if (d.Groups.Count > 2 || d.Groups.Distinct().Count() != d.Groups.Count || d.Groups.Any(x => !fields.TryGetValue(x, out var f) || !f.Groupable)) throw Invalid("Choose up to two supported grouping fields.");
		if (d.Layout == "summary" && d.Summaries.Count == 0) throw Invalid("Add at least one summary.");
		if (d.Summaries.Count > 12) throw Invalid("Choose at most 12 summaries.");
		foreach (var s in d.Summaries)
			if (!fields.TryGetValue(s.Field, out var f) || !dataset.SummaryFunctions.Contains(s.Function) || (s.Function is "sum" or "avg" && !f.Summarizable)) throw Invalid("Choose a supported field and summary function.");
		if (d.Summaries.Select(x => x.Function + "_" + x.Field).Distinct().Count() != d.Summaries.Count) throw Invalid("Remove duplicate summaries.");
		ValidateGroup(d.Filters, fields, 0);
		var generated = d.Summaries.Select(x => x.Function + "_" + x.Field).Append("rowCount").ToHashSet();
		if (d.Sorts.Count > 4 || d.Sorts.Any(x => (!fields.ContainsKey(x.Field) && !generated.Contains(x.Field)) || x.Direction is not ("asc" or "desc"))) throw Invalid("Choose up to four supported sort fields.");
		if (d.Layout == "summary" && d.Sorts.Any(x => !d.Groups.Contains(x.Field) && !generated.Contains(x.Field))) throw Invalid("Sort grouped summaries by a grouping field or a summary column.");
		if (d.Parameters.Distinct().Count() != d.Parameters.Count || d.Parameters.Any(x => !dataset.Parameters.Contains(x))) throw Invalid("A run parameter is not supported by this dataset.");
		if (d.DataSource is "attendance" or "leave" or "payroll_register" or "payroll_statement" && !d.Parameters.Contains("period")) throw Invalid("This dataset requires a reporting period parameter.");
		if (d.DataSource == "payroll_statement" && !d.Parameters.Contains("employeeIds")) throw Invalid("Salary statements require a staff parameter.");
		if (d.DataSource == "leave_balances" && !d.Parameters.Contains("academicYearId")) throw Invalid("Current leave balances require an academic year parameter.");
		if ((d.Layout == "attendanceMatrix" || d.DataSource is "payroll_register" or "payroll_statement") && d.DefaultPeriod is not ("thisMonth" or "lastMonth")) throw Invalid("Choose This month or Last month as the default for a monthly report.");
		if (d.Layout == "attendanceMatrix" && d.Columns.Any(x => x.Field is not ("employeeCode" or "employeeName" or "department" or "employeeId"))) throw Invalid("Monthly registers support staff identifier columns. Day columns are added automatically.");
		if (d.Layout is "attendanceMatrix" or "salaryComponents" && !d.Columns.Any(x => x.Field is "employeeCode" or "employeeName")) throw Invalid("Keep a staff code or name column in this register.");
		if (!string.IsNullOrEmpty(d.DateField) && (!fields.TryGetValue(d.DateField, out var date) || date.Type != "date")) throw Invalid("Choose a valid date basis.");
		if (d.DataSource != "employee" && !string.IsNullOrEmpty(d.DateField)) throw Invalid("This dataset uses its canonical reporting dates. Remove the custom date basis.");
		if (d.DataSource == "employee" && d.Parameters.Contains("period") && string.IsNullOrEmpty(d.DateField)) throw Invalid("Choose a staff date basis or remove the reporting period parameter.");
		if (d.PaperSize is not ("A4" or "A3") || d.Orientation is not ("portrait" or "landscape")) throw Invalid("Choose A4/A3 and portrait/landscape.");
		if (d.Chart is { Type: not "none" } c)
		{
			if (c.Type is not ("kpi" or "bar" or "line" or "donut") || c.Function is not ("count" or "distinct" or "sum" or "avg")) throw Invalid("Choose a supported chart and aggregation.");
			if (c.Type != "kpi" && (c.CategoryField == null || !fields.ContainsKey(c.CategoryField))) throw Invalid("Select the chart category.");
			if (c.Function != "count" && (c.ValueField == null || !fields.TryGetValue(c.ValueField, out var f) || (c.Function is "sum" or "avg" && !f.Summarizable))) throw Invalid("Select a valid chart value.");
		}
		return dataset;
	}
	private static void ValidateGroup(HrReportConditionGroupDto group, Dictionary<string, HrReportFieldDto> fields, int depth)
	{
		if (group == null || group.Conditions == null || group.Groups == null || group.Conditions.Any(x => x == null || string.IsNullOrWhiteSpace(x.Field))) throw Invalid("Provide complete filter groups and conditions.");
		if (depth > 1 || group.Match is not ("all" or "any") || group.Conditions.Count + group.Groups.Count > 20) throw Invalid("Use match all/any with at most one nested level and 20 conditions per group.");
		foreach (var c in group.Conditions)
		{
			if (!fields.TryGetValue(c.Field, out var f) || !f.Operators.Contains(c.Operator)) throw Invalid("A filter field or operator is unsupported.");
			if (c.Operator is "empty" or "notEmpty") { if (c.AskWhenGenerating) throw Invalid("Empty/not-empty rules do not need a generation parameter."); continue; }
			var values = c.Operator is "in" or "notIn" or "between" ? c.Values ?? [] : [c.Value ?? ""];
			if (values.Count > 500 || values.Any(x => x?.Length > 2048)) throw Invalid("Use up to 500 filter values, each no longer than 2,048 characters.");
			if (c.AskWhenGenerating && (values.Count == 0 || values.All(string.IsNullOrWhiteSpace))) continue;
			if (values.Count == 0 || (c.Operator == "between" && values.Count != 2) || values.Any(string.IsNullOrWhiteSpace)) throw Invalid("Enter all required filter values.");
			foreach (var value in values) _ = Parse(value, f.Type, strict: true);
		}
		foreach (var child in group.Groups) ValidateGroup(child, fields, depth + 1);
	}
	public static HrReportParametersDto ResolveParameters(HrReportDesignDto d, HrReportParametersDto p, DateTimeOffset now, string timezone)
	{
		Validate(d);
		if (p == null || p.EmployeeIds == null || p.Departments == null || p.FilterValues == null || p.Departments.Any(x => x == null)) throw Invalid("Provide valid report parameters.");
		_ = ResolveFilters(d, p);
		p.TimeZone = timezone;
		p.EmployeeIds = p.EmployeeIds.Distinct().ToList();
		p.Departments = p.Departments.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct().ToList();
		if (p.EmployeeGroup.HasValue && !Enum.IsDefined(typeof(ShikshaMitra.Common.EmployeeGroupType), p.EmployeeGroup.Value)) throw Invalid("Choose a supported staff group.");
		var supported = d.Parameters.ToHashSet();
		if ((!supported.Contains("employeeIds") && p.EmployeeIds.Count > 0) || (!supported.Contains("departments") && p.Departments.Count > 0)
			|| (!supported.Contains("includeDrafts") && p.IncludeDrafts) || (!supported.Contains("academicYearId") && p.AcademicYearId.HasValue)
			|| (!supported.Contains("educationMediumStageId") && p.EducationMediumStageId.HasValue) || (!supported.Contains("cycleId") && p.CycleId.HasValue)
			|| (!supported.Contains("employeeGroup") && p.EmployeeGroup.HasValue)) throw Invalid("The template does not expose this run parameter.");
		if (p.EmployeeIds.Count > 1000 || p.Departments.Count > 100 || p.Departments.Any(x => x.Length > 128)) throw Invalid("Narrow the staff selection.");
		if (d.DataSource == "leave_balances" && !p.AcademicYearId.HasValue) throw Invalid("Choose the academic year for current leave balances.");
		if (!supported.Contains("period")) { p.FromDate = null; p.ToDate = null; p.Month = null; return p; }
		var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(timezone)).Date);
		var first = new DateOnly(today.Year, today.Month, 1);
		p.Period = string.IsNullOrWhiteSpace(p.Period) ? d.DefaultPeriod : p.Period;
		(p.FromDate, p.ToDate) = p.Period switch
		{
			"today" => (today, today),
			"yesterday" => (today.AddDays(-1), today.AddDays(-1)),
			"last7Days" => (today.AddDays(-6), today),
			"thisMonth" => (first, first.AddMonths(1).AddDays(-1)),
			"lastMonth" => (first.AddMonths(-1), first.AddDays(-1)),
			"date" when p.FromDate.HasValue => (p.FromDate.Value, p.FromDate.Value),
			"academicYear" when p.AcademicYearId.HasValue && p.FromDate.HasValue && p.ToDate.HasValue => (p.FromDate.Value, p.ToDate.Value),
			"month" when DateOnly.TryParseExact(p.Month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var selected) => (selected, selected.AddMonths(1).AddDays(-1)),
			"custom" when p.FromDate.HasValue && p.ToDate.HasValue => (p.FromDate.Value, p.ToDate.Value),
			_ => throw Invalid("Choose a period or enter both dates.")
		};
		if (p.FromDate > p.ToDate || p.FromDate.Value.Year < 1900 || p.ToDate.Value.Year > 2200) throw Invalid("Enter a valid reporting range.");
		if (d.DataSource != "payroll_statement" && p.ToDate.Value.DayNumber - p.FromDate.Value.DayNumber > 365) throw Invalid("Limit the reporting period to 366 days.");
		if ((d.Layout == "attendanceMatrix" || d.DataSource == "payroll_register") && (p.FromDate.Value.Year != p.ToDate.Value.Year || p.FromDate.Value.Month != p.ToDate.Value.Month)) throw Invalid("Choose one month for this register.");
		if ((d.Layout == "attendanceMatrix" || d.DataSource == "payroll_register") && (p.FromDate.Value.Day != 1 || p.ToDate.Value.Day != DateTime.DaysInMonth(p.ToDate.Value.Year, p.ToDate.Value.Month))) throw Invalid("Choose a complete month for this register.");
		if (d.DataSource == "payroll_statement" && p.EmployeeIds.Count != 1) throw Invalid("Choose exactly one staff member for a salary statement.");
		if (d.DataSource == "payroll_statement")
		{
			p.FromDate = new DateOnly(p.FromDate.Value.Year, p.FromDate.Value.Month, 1);
			p.ToDate = new DateOnly(p.ToDate.Value.Year, p.ToDate.Value.Month, 1).AddMonths(1).AddDays(-1);
		}
		p.Month = p.FromDate.Value.ToString("yyyy-MM", CultureInfo.InvariantCulture);
		return p;
	}
	public static HrReportingResult Execute(HrReportDesignDto d, HrReportParametersDto parameters, List<Dictionary<string, object?>> source)
	{
		var dataset = Validate(d);
		if (source.Count > MaximumPopulation) throw new BusinessException(HRErrorCodes.ReportPopulationTooLarge);
		var filters = ResolveFilters(d, parameters);
		var filtered = source.Where(x => Matches(x, filters, dataset)).ToList();
		var result = new HrReportingResult { EmployeeIds = filtered.Where(x => x.ContainsKey("__employeeId")).Select(x => Guid.Parse(x["__employeeId"]!.ToString()!)).Distinct().ToList() };
		result.AssignmentIds = filtered.Where(x => x.ContainsKey("__assignmentId")).Select(x => Guid.Parse(x["__assignmentId"]!.ToString()!)).Distinct().ToList();
		result.Warnings = source.SelectMany(x => x.GetValueOrDefault("__warnings") as IEnumerable<string> ?? []).Distinct().ToList();
		if (source.Count == 0) result.Warnings.Add("No source records match this selection.");
		if (d.Chart is { Type: not "none" } chart) result.Chart = BuildChart(filtered, chart);
		var fields = dataset.Fields.ToDictionary(x => x.Id);
		if (d.Layout == "attendanceMatrix") return Matrix(d, parameters, filtered, result);
		if (d.Layout == "salaryComponents") return SalaryComponents(d, fields, filtered, result);
		var rows = filtered;
		if (d.Layout == "summary")
		{
			rows = filtered.GroupBy(row => JsonSerializer.Serialize(d.Groups.Select(f => row.GetValueOrDefault(f)), Json)).Select(group =>
			{
				var row = d.Groups.ToDictionary(f => f, f => group.First().GetValueOrDefault(f));
				row["rowCount"] = group.Count();
				foreach (var summary in d.Summaries) row[summary.Function + "_" + summary.Field] = Aggregate(group, summary.Function, summary.Field);
				return row;
			}).ToList();
			result.Columns = d.Groups.Select(f => fields[f]).Append(HrReportingCatalogue.Field("rowCount", "Rows", "number")).ToList();
			result.Columns.AddRange(d.Summaries.Select(s => HrReportingCatalogue.Field(s.Function + "_" + s.Field, SummaryLabel(s.Function) + " " + fields[s.Field].Label, s.Function is "count" or "distinct" ? "number" : fields[s.Field].Type)));
			result.Totals["rowCount"] = filtered.Count;
			foreach (var s in d.Summaries) result.Totals[s.Function + "_" + s.Field] = Aggregate(filtered, s.Function, s.Field);
		}
		else result.Columns = d.Columns.Select(c => new HrReportFieldDto { Id = c.Field, Label = string.IsNullOrWhiteSpace(c.Label) ? fields[c.Field].Label : c.Label, Type = c.Format == "auto" ? fields[c.Field].Type : c.Format }).ToList();
		foreach (var f in dataset.Fields.Where(x => x.Summarizable && d.Columns.Any(c => c.Field == x.Id))) result.Totals[f.Id] = Aggregate(filtered, "sum", f.Id);
		IOrderedEnumerable<Dictionary<string, object?>>? ordered = null;
		foreach (var sort in d.Sorts)
		{
			Func<Dictionary<string, object?>, object?> key = x => x.GetValueOrDefault(sort.Field);
			ordered = ordered == null ? sort.Direction == "desc" ? rows.OrderByDescending(key, CellComparer.Instance) : rows.OrderBy(key, CellComparer.Instance)
				: sort.Direction == "desc" ? ordered.ThenByDescending(key, CellComparer.Instance) : ordered.ThenBy(key, CellComparer.Instance);
		}
		result.Rows = (ordered?.ToList() ?? rows).Select(row => result.Columns.ToDictionary(c => c.Id, c => row.GetValueOrDefault(c.Id))).ToList();
		return result;
	}
	private static bool Matches(Dictionary<string, object?> row, HrReportConditionGroupDto group, HrReportDatasetDto dataset)
	{
		var tests = group.Conditions.Select(c => MatchesCondition(row, c, dataset)).Concat(group.Groups.Select(g => Matches(row, g, dataset))).ToList();
		return tests.Count == 0 || (group.Match == "all" ? tests.All(x => x) : tests.Any(x => x));
	}
	private static HrReportConditionGroupDto ResolveFilters(HrReportDesignDto d, HrReportParametersDto p)
	{
		var exposed = new HashSet<string>();
		HrReportConditionGroupDto Resolve(HrReportConditionGroupDto group, string path) => new()
		{
			Match = group.Match,
			Conditions = group.Conditions.Select((c, i) =>
			{
				if (!c.AskWhenGenerating) return c;
				var key = path + "." + i; exposed.Add(key);
				var supplied = p.FilterValues.GetValueOrDefault(key);
				var resolved = new HrReportFilterConditionDto { Field = c.Field, Operator = c.Operator, Value = supplied?.Value ?? c.Value, Values = supplied?.Values ?? c.Values };
				p.FilterValues[key] = new() { Value = resolved.Value, Values = resolved.Values };
				return resolved;
			}).ToList(),
			Groups = group.Groups.Select((g, i) => Resolve(g, path + ".g" + i)).ToList()
		};
		var result = Resolve(d.Filters, "filters");
		if (p.FilterValues.Keys.Any(key => !exposed.Contains(key))) throw Invalid("The template does not expose this filter parameter.");
		ValidateGroup(result, HrReportingCatalogue.Datasets().Single(x => x.Id == d.DataSource).Fields.ToDictionary(x => x.Id), 0);
		return result;
	}
	private static bool MatchesCondition(Dictionary<string, object?> row, HrReportFilterConditionDto c, HrReportDatasetDto dataset)
	{
		var actual = row.GetValueOrDefault(c.Field); var empty = actual == null || string.IsNullOrEmpty(actual.ToString());
		if (c.Operator == "empty") return empty;
		if (c.Operator == "notEmpty") return !empty;
		if (empty) return false;
		var type = dataset.Fields.Single(x => x.Id == c.Field).Type;
		var expected = Parse(c.Value, type);
		var compare = CellComparer.Instance.Compare(actual, expected);
		return c.Operator switch
		{
			"equals" => compare == 0,
			"notEquals" => compare != 0,
			"contains" => actual!.ToString()!.Contains(c.Value ?? "", StringComparison.OrdinalIgnoreCase),
			"gte" => compare >= 0,
			"lte" => compare <= 0,
			"gt" => compare > 0,
			"lt" => compare < 0,
			"in" => (c.Values ?? []).Any(x => CellComparer.Instance.Compare(actual, Parse(x, type)) == 0),
			"notIn" => (c.Values ?? []).All(x => CellComparer.Instance.Compare(actual, Parse(x, type)) != 0),
			"between" => CellComparer.Instance.Compare(actual, Parse(c.Values![0], type)) >= 0 && CellComparer.Instance.Compare(actual, Parse(c.Values[1], type)) <= 0,
			_ => false
		};
	}
	public static object? Parse(string? raw, string type, bool strict = false)
	{
		if (string.IsNullOrWhiteSpace(raw)) return null;
		if (type is "number" or "money") { if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var n)) return n; }
		else if (type == "date") { if (DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return date; }
		else if (type == "datetime")
		{
			if (strict && !raw.EndsWith("Z", StringComparison.OrdinalIgnoreCase) && !System.Text.RegularExpressions.Regex.IsMatch(raw, @"[+-]\d{2}:\d{2}$")) throw Invalid("Enter UTC or an explicit offset for date/time filter values.");
			if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt)) return dt;
		}
		else if (type == "boolean") { if (bool.TryParse(raw, out var b)) return b; if (raw is "Yes" or "No") return raw == "Yes"; }
		else return raw;
		if (strict) throw Invalid("Enter a valid " + type + " filter value.");
		return null;
	}
	private static decimal? Aggregate(IEnumerable<Dictionary<string, object?>> rows, string function, string field)
	{
		var values = rows.Select(r => r.GetValueOrDefault(field)).Where(x => x != null).ToList();
		if (function == "count") return rows.Count();
		if (function == "distinct") return values.Select(x => x!.ToString()).Distinct(StringComparer.OrdinalIgnoreCase).Count();
		var numbers = values.Select(x => decimal.TryParse(Convert.ToString(x, CultureInfo.InvariantCulture), NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? (decimal?) n : null).Where(x => x.HasValue).Select(x => x!.Value).ToList();
		return numbers.Count == 0 ? null : function == "avg" ? numbers.Average() : numbers.Sum();
	}
	private static HrReportChartPreviewDto BuildChart(List<Dictionary<string, object?>> rows, HrReportChartDefinitionDto c)
	{
		var points = c.Type == "kpi" ? new List<HrReportChartPointDto> { new() { Label = c.Title ?? "Total", Value = Aggregate(rows, c.Function ?? "count", c.ValueField ?? "") ?? 0 } }
			: rows.GroupBy(x => ChartLabel(x.GetValueOrDefault(c.CategoryField!))).Select(g => new HrReportChartPointDto { Label = g.Key, Value = Aggregate(g, c.Function ?? "count", c.ValueField ?? "") ?? 0 }).ToList();
		if (points.Count > 200) throw Invalid("The chart has more than 200 categories. Choose a broader category or narrow the report.");
		if (c.Type == "donut" && points.Any(x => x.Value < 0)) throw Invalid("Donut charts require non-negative values. Choose a bar chart for negative values.");
		return new() { Type = c.Type, Title = c.Title ?? "Summary", Points = c.Type == "line" ? points.OrderBy(x => x.Label).ToList() : points.OrderByDescending(x => x.Value).ToList() };
	}
	private static string ChartLabel(object? value) => value switch { DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), DateTimeOffset date => date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), _ => value?.ToString() ?? "Unspecified" };
	private static string SummaryLabel(string function) => function switch { "count" => "Count", "distinct" => "Distinct", "avg" => "Average", _ => "Total" };
	private static HrReportingResult Matrix(HrReportDesignDto d, HrReportParametersDto p, List<Dictionary<string, object?>> rows, HrReportingResult result)
	{
		var start = p.FromDate ?? throw Invalid("Choose a month."); var end = p.ToDate!.Value;
		var fields = HrReportingCatalogue.Datasets().Single(x => x.Id == "attendance").Fields.ToDictionary(x => x.Id);
		var statuses = new[] { "Present", "HalfDay", "Absent", "Approved leave", "Half-day approved leave", "Not recorded", "Missing punch", "Holiday", "Non-working day", "Open day", "Future" };
		result.Columns = d.Columns.Select(x => HrReportingCatalogue.Field(x.Field, string.IsNullOrEmpty(x.Label) ? fields[x.Field].Label : x.Label, "string")).ToList();
		for (var day = start; day <= end; day = day.AddDays(1)) result.Columns.Add(HrReportingCatalogue.Field(day.ToString("yyyy-MM-dd"), day.ToString("dd"), "string"));
		foreach (var name in statuses) result.Columns.Add(HrReportingCatalogue.Field(name, name == "HalfDay" ? "Half day" : name, "number"));
		result.Rows = rows.GroupBy(x => x["employeeId"]).Select(g =>
		{
			var row = d.Columns.ToDictionary(c => c.Field, c => g.First().GetValueOrDefault(c.Field));
			foreach (var day in g) row[((DateOnly) day["attendanceDate"]!).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)] = day["classification"];
			foreach (var name in statuses) row[name] = g.Count(x => Equals(x["classification"], name) || Equals(x["exception"], name));
			return row;
		}).ToList();
		foreach (var name in statuses) result.Totals[name] = Aggregate(result.Rows, "sum", name);
		return result;
	}
	private static HrReportingResult SalaryComponents(HrReportDesignDto d, Dictionary<string, HrReportFieldDto> fields, List<Dictionary<string, object?>> rows, HrReportingResult result)
	{
		var components = rows.SelectMany(x => x.GetValueOrDefault("__components") as List<HrReportingSavedComponent> ?? []).GroupBy(x => x.Key).OrderBy(x => x.First().Label).ToList();
		if (components.Count > 100) throw Invalid("This salary register contains more than 100 components. Narrow the staff selection.");
		result.Columns = d.Columns.Select(c => FieldColumn(c, fields[c.Field])).ToList();
		result.Columns.AddRange(components.Select(x => HrReportingCatalogue.Field(x.Key, x.First().Label, "money")));
		if (components.Any(x => !x.First().HasIdentity)) result.Warnings.Add("Older payroll evidence has no component identifiers. Those columns use exact saved labels and are marked unidentified in their field identity.");
		IEnumerable<Dictionary<string, object?>> ordered = rows;
		IOrderedEnumerable<Dictionary<string, object?>>? sorted = null;
		foreach (var sort in d.Sorts)
			sorted = sorted == null ? sort.Direction == "desc" ? rows.OrderByDescending(x => x.GetValueOrDefault(sort.Field), CellComparer.Instance) : rows.OrderBy(x => x.GetValueOrDefault(sort.Field), CellComparer.Instance)
				: sort.Direction == "desc" ? sorted.ThenByDescending(x => x.GetValueOrDefault(sort.Field), CellComparer.Instance) : sorted.ThenBy(x => x.GetValueOrDefault(sort.Field), CellComparer.Instance);
		result.Rows = (sorted ?? ordered).Select(source =>
		{
			var saved = (source.GetValueOrDefault("__components") as List<HrReportingSavedComponent> ?? []).GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.Sum(c => c.Amount));
			return result.Columns.ToDictionary(c => c.Id, c => fields.ContainsKey(c.Id) ? source.GetValueOrDefault(c.Id) : saved.TryGetValue(c.Id, out var amount) ? (object) amount : null);
		}).ToList();
		foreach (var f in result.Columns.Where(x => x.Type is "number" or "money")) result.Totals[f.Id] = Aggregate(result.Rows, "sum", f.Id);
		return result;
	}
	private static HrReportFieldDto FieldColumn(HrReportDisplayColumnDto c, HrReportFieldDto field) => new() { Id = c.Field, Label = string.IsNullOrWhiteSpace(c.Label) ? field.Label : c.Label, Type = c.Format == "auto" ? field.Type : c.Format };
	private sealed class CellComparer : IComparer<object?>
	{
		public static readonly CellComparer Instance = new();
		public int Compare(object? x, object? y)
		{
			if (x == null) return y == null ? 0 : -1; if (y == null) return 1;
			if (x is decimal a && y is decimal b) return a.CompareTo(b);
			if (x is int or long or decimal && y is int or long or decimal) return Convert.ToDecimal(x, CultureInfo.InvariantCulture).CompareTo(Convert.ToDecimal(y, CultureInfo.InvariantCulture));
			if (x is DateOnly da && y is DateOnly db) return da.CompareTo(db);
			if (x is DateTimeOffset ta && y is DateTimeOffset tb) return ta.CompareTo(tb);
			return string.Compare(Convert.ToString(x, CultureInfo.InvariantCulture), Convert.ToString(y, CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
		}
	}
}
