using System;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionOfferingColumnFilters
{
	public static IQueryable<T> Apply<T>(IQueryable<T> query, string? filters) where T : AdmissionOfferingColumnFilterRow
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
				var row = Expression.Parameter(typeof(T), "row");
				Expression predicate;
				if (column == "name")
				{
					if (valueType != "string" || operation is not ("contains" or "equals" or "startsWith" or "endsWith") ||
						value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()) || value.GetString()!.Length > 150) throw Invalid();
					var field = Expression.Call(Expression.Property(row, nameof(AdmissionOfferingColumnFilterRow.Name)), nameof(string.ToLower), Type.EmptyTypes);
					var term = Expression.Constant(value.GetString()!.ToLowerInvariant());
					predicate = operation == "equals" ? Expression.Equal(field, term) :
						Expression.Call(field, operation switch { "contains" => nameof(string.Contains), "startsWith" => nameof(string.StartsWith), _ => nameof(string.EndsWith) }, Type.EmptyTypes, term);
				}
				else if (column is "effectiveOpenDate" or "effectiveCloseDate")
				{
					if (valueType != "date" || operation is not ("equals" or "after" or "before") || value.ValueKind != JsonValueKind.String ||
						!DateTime.TryParseExact(value.GetString(), ["dd-MM-yyyy", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) throw Invalid();
					var field = Expression.Call(typeof(DateOnly), nameof(DateOnly.FromDateTime), Type.EmptyTypes,
						Expression.Property(row, column == "effectiveOpenDate" ? nameof(AdmissionOfferingColumnFilterRow.EffectiveOpenDate) : nameof(AdmissionOfferingColumnFilterRow.EffectiveCloseDate)));
					var term = Expression.Constant(DateOnly.FromDateTime(date));
					predicate = operation switch { "equals" => Expression.Equal(field, term), "after" => Expression.GreaterThan(field, term), _ => Expression.LessThan(field, term) };
				}
				else if (column == "applicationCount")
				{
					if (valueType != "number" || operation is not ("equals" or "greaterThan" or "lessThan" or "between")) throw Invalid();
					var field = Expression.Property(row, nameof(AdmissionOfferingColumnFilterRow.ApplicationCount));
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
				query = query.Where(Expression.Lambda<Func<T, bool>>(predicate, row));
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

	private static BusinessException Invalid() => new("Students:Admissions:InvalidOfferingFilter", "One or more admission offering filters are invalid.");
}

public class AdmissionOfferingColumnFilterRow
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public DateTime EffectiveOpenDate { get; set; }
	public DateTime EffectiveCloseDate { get; set; }
	public int ApplicationCount { get; set; }
}
