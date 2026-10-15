using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using Volo.Abp.Validation;

namespace ShikshaMitra.Finance.Common;

public enum FinanceTableValueType { Text, Number, Date, Enum }
public sealed record FinanceTableColumn(FinanceTableValueType Type, bool Sortable = true, int[]? Values = null);
public sealed class FinanceTableFilter
{
    public string Column { get; set; } = "";
    public string Type { get; set; } = "";
    public JsonElement Value { get; set; }
}

/// <summary>Read-only allowlisted table queries. Alternatives in one column are ORed; columns are ANDed.</summary>
public sealed class FinanceTableQuery
{
    public string? Search { get; private init; }
    public string? SortBy { get; private init; }
    public bool Descending { get; private init; }
    public IReadOnlyList<FinanceTableFilter> Filters { get; private init; } = [];

    public static FinanceTableQuery Parse(string? json, string? search, string? sortBy, string? sortOrder,
        IReadOnlyDictionary<string, FinanceTableColumn> columns)
    {
        try
        {
            if ((json?.Length ?? 0) > 16000 || (search?.Length ?? 0) > 300 ||
                (!string.IsNullOrEmpty(sortBy) && (!columns.TryGetValue(sortBy, out var column) || !column.Sortable)) ||
                (!string.IsNullOrEmpty(sortOrder) && sortOrder is not ("asc" or "desc"))) throw new FormatException();
            var filters = string.IsNullOrWhiteSpace(json) ? [] :
                JsonSerializer.Deserialize<List<FinanceTableFilter>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new FormatException();
            if (filters.Count > 30 || filters.Any(f => f is null || !Valid(f, columns))) throw new FormatException();
            return new FinanceTableQuery { Search = search?.Trim(), SortBy = string.IsNullOrEmpty(sortBy) ? null : sortBy,
                Descending = sortOrder == "desc", Filters = filters };
        }
        catch (Exception ex) when (ex is FormatException or JsonException or InvalidOperationException)
        {
            throw new AbpValidationException("Invalid finance table query.",
                [new ValidationResult("Choose a supported column, operator and value.", ["Filters", "SortBy"])]);
        }
    }

    private static decimal Number(JsonElement value) => decimal.Parse(value.ToString(), CultureInfo.InvariantCulture);
    private static bool Numeric(JsonElement value) => decimal.TryParse(value.ToString(), NumberStyles.Number,
        CultureInfo.InvariantCulture, out _);
    private static bool Valid(FinanceTableFilter filter, IReadOnlyDictionary<string, FinanceTableColumn> columns)
    {
        if (!columns.TryGetValue(filter.Column, out var column) || filter.Value.ToString().Length > 300) return false;
        return column.Type switch
        {
            FinanceTableValueType.Text => filter.Value.ValueKind == JsonValueKind.String &&
                filter.Type is "contains" or "equals" or "startsWith" or "endsWith",
            FinanceTableValueType.Number => filter.Type == "between"
                ? filter.Value.ValueKind == JsonValueKind.Object && filter.Value.TryGetProperty("min", out var min) &&
                    filter.Value.TryGetProperty("max", out var max) && Numeric(min) && Numeric(max) && Number(min) <= Number(max)
                : filter.Type is "equals" or "greaterThan" or "lessThan" && Numeric(filter.Value),
            FinanceTableValueType.Enum => filter.Type == "equals" && int.TryParse(filter.Value.ToString(), out var value) &&
                column.Values is not null && Enumerable.Contains(column.Values, value),
            FinanceTableValueType.Date => filter.Type is "equals" or "after" or "before" &&
                DateTimeOffset.TryParse(filter.Value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            _ => false
        };
    }

    public IQueryable<T> Apply<T>(IQueryable<T> query, IReadOnlyDictionary<string, LambdaExpression> fields)
    {
        foreach (var group in Filters.GroupBy(f => f.Column))
        {
            var field = fields[group.Key];
            Expression? alternatives = null;
            foreach (var filter in group)
            {
                var predicate = Build(filter, field.Body);
                alternatives = alternatives is null ? predicate : Expression.OrElse(alternatives, predicate);
            }
            query = query.Where(Expression.Lambda<Func<T, bool>>(alternatives!, field.Parameters));
        }
        return query;
    }

    public IQueryable<T> Order<T>(IQueryable<T> query, IReadOnlyDictionary<string, LambdaExpression> fields,
        string defaultColumn, bool defaultDescending, Expression<Func<T, Guid>> identity)
    {
        var field = fields[SortBy ?? defaultColumn];
        var descending = SortBy is null ? defaultDescending : Descending;
        var order = Expression.Call(typeof(Queryable), descending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy),
            [typeof(T), field.ReturnType], query.Expression, Expression.Quote(field));
        var stable = Expression.Call(typeof(Queryable), nameof(Queryable.ThenBy), [typeof(T), typeof(Guid)], order,
            Expression.Quote(identity));
        return query.Provider.CreateQuery<T>(stable);
    }

    private static Expression Build(FinanceTableFilter filter, Expression field)
    {
        if (Nullable.GetUnderlyingType(field.Type) is not null)
            return Expression.AndAlso(Expression.Property(field, "HasValue"),
                Build(filter, Expression.Property(field, "Value")));
        if (field.Type == typeof(string))
        {
            var text = Expression.Call(Expression.Coalesce(field, Expression.Constant("")), nameof(string.ToLower), System.Type.EmptyTypes);
            var value = Expression.Constant(filter.Value.GetString()!.ToLowerInvariant());
            return filter.Type == "equals" ? Expression.Equal(text, value) : Expression.Call(text, filter.Type switch
            {
                "startsWith" => nameof(string.StartsWith), "endsWith" => nameof(string.EndsWith), _ => nameof(string.Contains)
            }, System.Type.EmptyTypes, value);
        }
        if (field.Type == typeof(DateTimeOffset) || field.Type == typeof(DateTime))
        {
            var day = DateTimeOffset.Parse(filter.Value.ToString(), CultureInfo.InvariantCulture);
            var startDay = new DateTimeOffset(day.Date, day.Offset).ToUniversalTime();
            var endDay = startDay.AddDays(1);
            var start = field.Type == typeof(DateTime) ? Expression.Constant(startDay.UtcDateTime) : Expression.Constant(startDay);
            var end = field.Type == typeof(DateTime) ? Expression.Constant(endDay.UtcDateTime) : Expression.Constant(endDay);
            return filter.Type switch
            {
                "after" => Expression.GreaterThanOrEqual(field, start),
                "before" => Expression.LessThan(field, end),
                _ => Expression.AndAlso(Expression.GreaterThanOrEqual(field, start), Expression.LessThan(field, end))
            };
        }
        // Int32 counts/statuses are exactly representable as doubles. Avoid converting them
        // to decimal TEXT on SQLite, where CAST(0 AS TEXT) does not equal a decimal '0.0'.
        var integer = field.Type == typeof(int);
        var numeric = integer ? Expression.Convert(field, typeof(double)) :
            field.Type == typeof(decimal) ? field : Expression.Convert(field, typeof(decimal));
        Expression Bound(JsonElement value) => integer ? Expression.Constant((double)Number(value)) : Expression.Constant(Number(value));
        if (filter.Type == "between") return Expression.AndAlso(
            Expression.GreaterThanOrEqual(numeric, Bound(filter.Value.GetProperty("min"))),
            Expression.LessThanOrEqual(numeric, Bound(filter.Value.GetProperty("max"))));
        var number = Bound(filter.Value);
        return filter.Type switch
        {
            "greaterThan" => Expression.GreaterThan(numeric, number),
            "lessThan" => Expression.LessThan(numeric, number),
            _ => Expression.Equal(numeric, number)
        };
    }
}
