using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using Volo.Abp.Validation;

namespace ShikshaMitra.Finance.Fees.Charges;

/// <summary>Allowlisted read-only filters: OR within a column, AND across columns.</summary>
public sealed class ChargeColumnFilter
{
    public string Column { get; set; } = "";
    public string Type { get; set; } = "";
    public JsonElement Value { get; set; }

    private static decimal Number(JsonElement value) => decimal.Parse(value.ToString(), CultureInfo.InvariantCulture);
    private static bool IsNumber(JsonElement value) => decimal.TryParse(value.ToString(), NumberStyles.Number,
        CultureInfo.InvariantCulture, out _);

    public static List<ChargeColumnFilter> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            if (json.Length > 16000) throw new FormatException();
            var filters = JsonSerializer.Deserialize<List<ChargeColumnFilter>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new FormatException();
            if (filters.Count > 30 || filters.Any(f => f is null || !f.IsValid())) throw new FormatException();
            return filters;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException)
        {
            throw new AbpValidationException("Invalid charge column filters.",
                [new ValidationResult("Choose a supported column, operator and value.", ["Filters"])]);
        }
    }

    private bool IsValid()
    {
        if (Value.ToString().Length > 300) return false;
        return Column switch
        {
            "chargeName" or "categoryName" or "description" => Value.ValueKind == JsonValueKind.String &&
                Type is "contains" or "equals" or "startsWith" or "endsWith",
            "studentCount" or "amount" => Type == "between"
                ? Value.ValueKind == JsonValueKind.Object && Value.TryGetProperty("min", out var min) &&
                    Value.TryGetProperty("max", out var max) && IsNumber(min) && IsNumber(max) && Number(min) <= Number(max)
                : Type is "equals" or "greaterThan" or "lessThan" && IsNumber(Value),
            "workflowStatus" => Type == "equals" && int.TryParse(Value.ToString(), out var status) &&
                Enum.IsDefined(typeof(ChargeWorkflowStatus), status),
            "dueDate" => Type is "equals" or "after" or "before" &&
                DateTimeOffset.TryParse(Value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            _ => false
        };
    }

    public static bool MatchesStatus(IReadOnlyCollection<ChargeColumnFilter> filters, ChargeWorkflowStatus status) =>
        !filters.Any(f => f.Column == "workflowStatus") ||
        filters.Any(f => f.Column == "workflowStatus" && int.Parse(f.Value.ToString()) == (int)status);

    public static IQueryable<T> Apply<T>(IQueryable<T> query, IReadOnlyCollection<ChargeColumnFilter> filters,
        IReadOnlyDictionary<string, LambdaExpression> fields)
    {
        foreach (var group in filters.Where(f => f.Column != "workflowStatus").GroupBy(f => f.Column))
        {
            var field = fields[group.Key];
            Expression? alternatives = null;
            foreach (var filter in group)
            {
                var predicate = filter.Build(field.Body);
                alternatives = alternatives is null ? predicate : Expression.OrElse(alternatives, predicate);
            }
            query = query.Where(Expression.Lambda<Func<T, bool>>(alternatives!, field.Parameters));
        }
        return query;
    }

    private Expression Build(Expression field)
    {
        if (field.Type == typeof(string))
        {
            var text = Expression.Call(Expression.Coalesce(field, Expression.Constant("")), nameof(string.ToLower), System.Type.EmptyTypes);
            var value = Expression.Constant(Value.GetString()!.ToLowerInvariant());
            return Type == "equals" ? Expression.Equal(text, value) : Expression.Call(text, Type switch
            {
                "startsWith" => nameof(string.StartsWith), "endsWith" => nameof(string.EndsWith), _ => nameof(string.Contains)
            }, System.Type.EmptyTypes, value);
        }
        if (field.Type == typeof(DateTimeOffset))
        {
            var day = DateTimeOffset.Parse(Value.ToString(), CultureInfo.InvariantCulture);
            var start = Expression.Constant(new DateTimeOffset(day.Date, day.Offset).ToUniversalTime());
            var end = Expression.Constant(new DateTimeOffset(day.Date.AddDays(1), day.Offset).ToUniversalTime());
            return Type switch
            {
                "after" => Expression.GreaterThanOrEqual(field, start),
                "before" => Expression.LessThan(field, end),
                _ => Expression.AndAlso(Expression.GreaterThanOrEqual(field, start), Expression.LessThan(field, end))
            };
        }
        var numeric = field.Type == typeof(decimal) ? field : Expression.Convert(field, typeof(decimal));
        if (Type == "between") return Expression.AndAlso(
            Expression.GreaterThanOrEqual(numeric, Expression.Constant(Number(Value.GetProperty("min")))),
            Expression.LessThanOrEqual(numeric, Expression.Constant(Number(Value.GetProperty("max")))));
        var number = Expression.Constant(Number(Value));
        return Type switch
        {
            "greaterThan" => Expression.GreaterThan(numeric, number),
            "lessThan" => Expression.LessThan(numeric, number),
            _ => Expression.Equal(numeric, number)
        };
    }
}
