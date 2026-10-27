using System;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionCycleColumnFilters
{
	public static IQueryable<AdmissionCycleColumnFilterRow> Apply(IQueryable<AdmissionCycleColumnFilterRow> query, string? filters)
	{
		if (string.IsNullOrWhiteSpace(filters)) return query;
		if (filters.Length > 12000) throw Invalid();
		try
		{
			using var document = JsonDocument.Parse(filters);
			if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() > 20) throw Invalid();
			foreach (var filter in document.RootElement.EnumerateArray())
			{
				if (filter.ValueKind != JsonValueKind.Object || !filter.TryGetProperty("value", out var value)) throw Invalid();
				var column = Text(filter, "column");
				var operation = Text(filter, "type");
				var valueType = Text(filter, "valueType");
				var row = Expression.Parameter(typeof(AdmissionCycleColumnFilterRow), "row");
				Expression predicate;
				if (column == "name")
				{
					if (valueType != "string" || operation is not ("contains" or "equals" or "startsWith" or "endsWith") ||
						value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()) || value.GetString()!.Length > 150) throw Invalid();
					var field = Expression.Call(Expression.Property(row, nameof(AdmissionCycleColumnFilterRow.Name)), nameof(string.ToLower), Type.EmptyTypes);
					var term = Expression.Constant(value.GetString()!.ToLowerInvariant());
					predicate = operation == "equals" ? Expression.Equal(field, term) :
						Expression.Call(field, operation switch { "contains" => nameof(string.Contains), "startsWith" => nameof(string.StartsWith), _ => nameof(string.EndsWith) }, Type.EmptyTypes, term);
				}
				else if (column is "startDate" or "endDate")
				{
					if (valueType != "date" || operation is not ("equals" or "after" or "before") || value.ValueKind != JsonValueKind.String ||
						!DateTime.TryParseExact(value.GetString(), ["dd-MM-yyyy", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) throw Invalid();
					var field = Expression.Property(Expression.Property(row, column == "startDate" ? nameof(AdmissionCycleColumnFilterRow.StartDate) : nameof(AdmissionCycleColumnFilterRow.EndDate)), nameof(DateTime.Date));
					var term = Expression.Constant(DateTime.SpecifyKind(date, DateTimeKind.Utc));
					predicate = operation switch { "equals" => Expression.Equal(field, term), "after" => Expression.GreaterThan(field, term), _ => Expression.LessThan(field, term) };
				}
				else if (column is "linkedOfferingCount" or "applicationCount")
				{
					if (valueType != "number" || operation is not ("equals" or "greaterThan" or "lessThan" or "between")) throw Invalid();
					var field = Expression.Property(row, column == "linkedOfferingCount" ? nameof(AdmissionCycleColumnFilterRow.LinkedOfferingCount) : nameof(AdmissionCycleColumnFilterRow.ApplicationCount));
					if (operation == "between")
					{
						if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("min", out var minimum) || !value.TryGetProperty("max", out var maximum)) throw Invalid();
						var min = Number(minimum);
						var max = Number(maximum);
						if (min > max) throw Invalid();
						predicate = Expression.AndAlso(Expression.GreaterThanOrEqual(field, Expression.Constant(min)), Expression.LessThanOrEqual(field, Expression.Constant(max)));
					}
					else
					{
						var term = Expression.Constant(Number(value));
						predicate = operation switch { "equals" => Expression.Equal(field, term), "greaterThan" => Expression.GreaterThan(field, term), _ => Expression.LessThan(field, term) };
					}
				}
				else throw Invalid();
				query = query.Where(Expression.Lambda<Func<AdmissionCycleColumnFilterRow, bool>>(predicate, row));
			}
			return query;
		}
		catch (JsonException) { throw Invalid(); }
	}

	private static string Text(JsonElement element, string property) =>
		element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : throw Invalid();

	private static int Number(JsonElement value)
	{
		var raw = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
		return value.ValueKind is JsonValueKind.Number or JsonValueKind.String &&
			int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= 0 ? number : throw Invalid();
	}

	private static BusinessException Invalid() => new("Students:Admissions:InvalidCycleFilter", "One or more admission period filters are invalid.");
}

public class AdmissionCycleColumnFilterRow
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
	public int LinkedOfferingCount { get; set; }
	public int ApplicationCount { get; set; }
}
