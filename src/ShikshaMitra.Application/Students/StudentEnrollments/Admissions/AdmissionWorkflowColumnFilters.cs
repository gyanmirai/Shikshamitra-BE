using System;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionWorkflowColumnFilters
{
	public static IQueryable<AdmissionWorkflowColumnFilterRow> Apply(IQueryable<AdmissionWorkflowColumnFilterRow> query, string? filters)
	{
		if (filters?.Length > 12000) throw Invalid();
		if (string.IsNullOrWhiteSpace(filters)) return query;
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
				var row = Expression.Parameter(typeof(AdmissionWorkflowColumnFilterRow), "row");
				Expression predicate;
				if (column == "name")
				{
					if (valueType != "string" || operation is not ("contains" or "equals" or "startsWith" or "endsWith") ||
						value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()) || value.GetString()!.Length > 150) throw Invalid();
					var field = Expression.Call(Expression.Property(row, nameof(AdmissionWorkflowColumnFilterRow.Name)), nameof(string.ToLower), Type.EmptyTypes);
					var term = Expression.Constant(value.GetString()!.ToLowerInvariant());
					predicate = operation == "equals" ? Expression.Equal(field, term) :
						Expression.Call(field, operation switch { "contains" => nameof(string.Contains), "startsWith" => nameof(string.StartsWith), _ => nameof(string.EndsWith) }, Type.EmptyTypes, term);
				}
				else if (column is "lastModificationTime" or "publishedAt")
				{
					if (valueType != "date" || operation is not ("equals" or "after" or "before") || value.ValueKind != JsonValueKind.String ||
						!DateTime.TryParseExact(value.GetString(), ["dd-MM-yyyy", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) throw Invalid();
					var property = Expression.Property(row, column == "lastModificationTime" ? nameof(AdmissionWorkflowColumnFilterRow.LastModificationTime) : nameof(AdmissionWorkflowColumnFilterRow.PublishedAt));
					var field = column == "publishedAt" ? Expression.Property(property, nameof(Nullable<DateTime>.Value)) : property;
					var start = DateTime.SpecifyKind(date, DateTimeKind.Utc).AddMinutes(Offset(filter, "startOffsetMinutes"));
					var end = DateTime.SpecifyKind(date.AddDays(1), DateTimeKind.Utc).AddMinutes(Offset(filter, "endOffsetMinutes"));
					if ((end - start).TotalHours is < 22 or > 26) throw Invalid();
					predicate = operation switch
					{
						"equals" => Expression.AndAlso(Expression.GreaterThanOrEqual(field, Expression.Constant(start)), Expression.LessThan(field, Expression.Constant(end))),
						"after" => Expression.GreaterThanOrEqual(field, Expression.Constant(end)),
						_ => Expression.LessThan(field, Expression.Constant(start))
					};
					if (column == "publishedAt") predicate = Expression.AndAlso(Expression.Property(property, nameof(Nullable<DateTime>.HasValue)), predicate);
				}
				else if (column is "offeringCount" or "version")
				{
					if (valueType != "number" || operation is not ("equals" or "greaterThan" or "lessThan" or "between")) throw Invalid();
					var field = Expression.Property(row, column == "offeringCount" ? nameof(AdmissionWorkflowColumnFilterRow.OfferingCount) : nameof(AdmissionWorkflowColumnFilterRow.Version));
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
				query = query.Where(Expression.Lambda<Func<AdmissionWorkflowColumnFilterRow, bool>>(predicate, row));
			}
			return query;
		}
		catch (JsonException) { throw Invalid(); }
		catch (ArgumentOutOfRangeException) { throw Invalid(); }
	}

	private static string Text(JsonElement element, string property) =>
		element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : throw Invalid();

	private static int Number(JsonElement value)
	{
		var raw = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
		return value.ValueKind is JsonValueKind.Number or JsonValueKind.String &&
			int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= 0 ? number : throw Invalid();
	}

	private static int Offset(JsonElement filter, string property) =>
		filter.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var offset) && offset is >= -840 and <= 840
			? offset : throw Invalid();

	private static BusinessException Invalid() => new("Students:Admissions:InvalidWorkflowFilter", "One or more admission workflow filters are invalid.");
}

public class AdmissionWorkflowColumnFilterRow
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public DateTime LastModificationTime { get; set; }
	public DateTime? PublishedAt { get; set; }
	public int OfferingCount { get; set; }
	public int Version { get; set; }
}

