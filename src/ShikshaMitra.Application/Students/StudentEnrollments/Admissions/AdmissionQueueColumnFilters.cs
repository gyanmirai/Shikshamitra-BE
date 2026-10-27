using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Globalization;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

// Read-only list predicates. Call only after tenant/permission/academic scope and before count/paging.
public static class AdmissionQueueColumnFilters
{
    public static IQueryable<AdmissionApplicationListRow> Applications(IQueryable<AdmissionApplicationListRow> query, string? filters) => Apply(query, filters,
        new Dictionary<string, Expression<Func<AdmissionApplicationListRow, string?>>>
        { ["standardName"] = row => row.StandardName },
        new Dictionary<string, Expression<Func<AdmissionApplicationListRow, DateTime?>>>
        { ["decisionEnteredAt"] = row => row.DecisionEnteredAt, ["lastSavedAt"] = row => row.Application.LastSavedAt });

    public static IQueryable<AdmissionPipelineItemDto> Pipeline(IQueryable<AdmissionPipelineItemDto> query, string? filters) => Apply(query, filters,
        new Dictionary<string, Expression<Func<AdmissionPipelineItemDto, string?>>>
        {
            ["applicantName"] = row => row.ApplicantName, ["applicationNumber"] = row => row.ApplicationNumber,
            ["inquiryNumber"] = row => row.InquiryNumber, ["standardName"] = row => row.StandardName,
            ["contactPhone"] = row => row.ContactPhone, ["contactEmail"] = row => row.ContactEmail
        }, new Dictionary<string, Expression<Func<AdmissionPipelineItemDto, DateTime?>>>());

    public static IQueryable<T> Text<T>(IQueryable<T> query, Expression<Func<T, string?>> selector, string value, string operation)
    {
        if (operation is not ("contains" or "equals" or "startsWith" or "endsWith") || string.IsNullOrWhiteSpace(value) || value.Length > 250) throw Invalid();
        var field = selector.Body;
        var lower = Expression.Call(field, nameof(string.ToLower), Type.EmptyTypes);
        var term = Expression.Constant(value.Trim().ToLowerInvariant());
        var match = operation == "equals" ? (Expression)Expression.Equal(lower, term) : Expression.Call(lower,
            operation switch { "contains" => nameof(string.Contains), "startsWith" => nameof(string.StartsWith), _ => nameof(string.EndsWith) }, Type.EmptyTypes, term);
        return query.Where(Expression.Lambda<Func<T, bool>>(Expression.AndAlso(Expression.NotEqual(field, Expression.Constant(null, typeof(string))), match), selector.Parameters));
    }

    private static IQueryable<T> Apply<T>(IQueryable<T> query, string? filters,
        Dictionary<string, Expression<Func<T, string?>>> text, Dictionary<string, Expression<Func<T, DateTime?>>> dates)
    {
        if (string.IsNullOrWhiteSpace(filters)) return query;
        if (filters.Length > 12000) throw Invalid();
        try
        {
            using var document = JsonDocument.Parse(filters);
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() > 20) throw Invalid();
            foreach (var filter in document.RootElement.EnumerateArray())
            {
                var column = String(filter, "column");
                var operation = String(filter, "type");
                var kind = String(filter, "valueType");
                var value = String(filter, "value");
                if (text.TryGetValue(column, out var selector))
                {
                    if (kind != "string") throw Invalid();
                    query = Text(query, selector, value, operation);
                }
                else if (dates.TryGetValue(column, out var dateSelector))
                {
                    if (kind != "date" || operation is not ("equals" or "before" or "after") ||
                        !DateTime.TryParseExact(value, ["dd-MM-yyyy", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)) throw Invalid();
                    var startOffset = Offset(filter, "startOffsetMinutes");
                    var endOffset = Offset(filter, "endOffsetMinutes");
                    var start = DateTime.SpecifyKind(day.AddMinutes(startOffset), DateTimeKind.Utc);
                    var end = DateTime.SpecifyKind(day.AddDays(1).AddMinutes(endOffset), DateTimeKind.Utc);
                    if ((end - start).TotalHours is < 22 or > 26) throw Invalid();
                    var field = dateSelector.Body;
                    var hasValue = Expression.Property(field, nameof(Nullable<DateTime>.HasValue));
                    var timestamp = Expression.Property(field, nameof(Nullable<DateTime>.Value));
                    var startValue = Expression.Constant(start);
                    var endValue = Expression.Constant(end);
                    var match = operation switch
                    {
                        "before" => (Expression)Expression.LessThan(timestamp, startValue),
                        "after" => Expression.GreaterThanOrEqual(timestamp, endValue),
                        _ => Expression.AndAlso(Expression.GreaterThanOrEqual(timestamp, startValue), Expression.LessThan(timestamp, endValue))
                    };
                    query = query.Where(Expression.Lambda<Func<T, bool>>(Expression.AndAlso(hasValue, match), dateSelector.Parameters));
                }
                else throw Invalid();
            }
            return query;
        }
        catch (JsonException) { throw Invalid(); }
        catch (ArgumentOutOfRangeException) { throw Invalid(); }
    }

    private static string String(JsonElement filter, string name) => filter.ValueKind == JsonValueKind.Object && filter.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : throw Invalid();
    private static int Offset(JsonElement filter, string name) => filter.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var offset) && offset is >= -840 and <= 840 ? offset : throw Invalid();
    private static BusinessException Invalid() => new("Students:Admissions:InvalidQueueFilter", "One or more admission queue filters are invalid.");
}
