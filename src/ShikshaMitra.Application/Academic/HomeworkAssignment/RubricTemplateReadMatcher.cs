using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Academic.HomeworkAssignment;

internal static class RubricTemplateReadMatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly string[] Columns = ["title", "scoringModel", "appliesToWorkType", "criteria", "totalMarks", "usageCount", "lastModificationTime", "status"];

    public static List<RubricTemplateSummaryDto> Apply(List<RubricTemplateSummaryDto> source, GetRubricTemplatePageInput input)
    {
        var zone = string.IsNullOrWhiteSpace(input.TimeZoneId) ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(input.TimeZoneId);
        var filters = Parse<TemplateFilter>(input.Filters);
        var sorts = Parse<TemplateSort>(input.Sorts);
        if (filters.Any(filter => !Columns.Contains(filter.Column)) || sorts.Any(sort => !Columns.Contains(sort.Column)))
            throw new BusinessException("AssessmentRubric:UnsupportedListColumn");
        IEnumerable<RubricTemplateSummaryDto> rows = source.OrderBy(row => row.Title);
        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var search = input.Search.Trim();
            rows = rows.Where(row => Columns.Any(column => SearchText(row, column, zone).Any(text => text.Contains(search, StringComparison.OrdinalIgnoreCase))));
        }
        rows = rows.Where(row => filters.GroupBy(filter => filter.Column).All(group => group.Any(filter => Matches(row, filter, zone))));
        IOrderedEnumerable<RubricTemplateSummaryDto>? ordered = null;
        var comparer = Comparer<object?>.Create(Compare);
        foreach (var sort in sorts)
        {
            var ascending = sort.Direction != "desc";
            ordered = ordered == null
                ? ascending ? rows.OrderBy(row => Value(row, sort.Column), comparer) : rows.OrderByDescending(row => Value(row, sort.Column), comparer)
                : ascending ? ordered.ThenBy(row => Value(row, sort.Column), comparer) : ordered.ThenByDescending(row => Value(row, sort.Column), comparer);
        }
        return (ordered ?? rows).ToList();
    }

    private static bool Matches(RubricTemplateSummaryDto row, TemplateFilter filter, TimeZoneInfo zone)
    {
        var value = Value(row, filter.Column);
        if (filter.ValueType == "enum") return Convert.ToString(value, CultureInfo.InvariantCulture) == filter.Value.ToString();
        if (filter.ValueType is "number" or "formattedNumber")
        {
            if (value == null) return false;
            var actual = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (filter.Type == "between")
                return filter.Value.ValueKind == JsonValueKind.Object && filter.Value.TryGetProperty("min", out var min) &&
                    filter.Value.TryGetProperty("max", out var max) && Number(min, out var lower) && Number(max, out var upper) &&
                    lower <= upper && actual >= lower && actual <= upper;
            if (!Number(filter.Value, out var expected)) return false;
            return filter.Type switch { "equals" => actual == expected, "greaterThan" => actual > expected, "lessThan" => actual < expected, _ => false };
        }
        if (filter.ValueType is "date" or "dateTime")
        {
            if (!row.LastModificationTime.HasValue || !DateTimeOffset.TryParse(filter.Value.ToString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var expected)) return false;
            var actualDate = Local(row.LastModificationTime.Value, zone).Date;
            var expectedDate = TimeZoneInfo.ConvertTime(expected, zone).Date;
            return filter.Type switch { "equals" => actualDate == expectedDate, "after" => actualDate >= expectedDate, "before" => actualDate <= expectedDate, _ => false };
        }
        var text = Display(row, filter.Column, zone);
        var target = filter.Value.ToString();
        return filter.Type switch
        {
            "contains" => text.Contains(target, StringComparison.OrdinalIgnoreCase),
            "equals" => text.Equals(target, StringComparison.OrdinalIgnoreCase),
            "startsWith" => text.StartsWith(target, StringComparison.OrdinalIgnoreCase),
            "endsWith" => text.EndsWith(target, StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    private static object? Value(RubricTemplateSummaryDto row, string column) => column switch
    {
        "title" => row.Title, "scoringModel" => (int)row.ScoringModel, "appliesToWorkType" => (int?)row.AppliesToWorkType,
        "criteria" => row.CriteriaCount, "totalMarks" => (double)row.TotalMarks, "usageCount" => row.UsageCount,
        "lastModificationTime" => row.LastModificationTime?.Ticks ?? 0, "status" => (int)row.Status, _ => null
    };

    private static string Display(RubricTemplateSummaryDto row, string column, TimeZoneInfo zone) => column switch
    {
        "title" => $"{row.Title} Version {row.Version}",
        "scoringModel" => row.ScoringModel switch
        {
            RubricScoringModel.PointsBased => "Points-Based", RubricScoringModel.WeightedPercentage => "Weighted Percentage",
            RubricScoringModel.PerformanceLevels => "Performance Levels", RubricScoringModel.RatingScale => "Rating Scale", _ => "Checklist"
        },
        "appliesToWorkType" => row.AppliesToWorkType?.ToString() ?? "Both",
        "totalMarks" => row.ScoringModel == RubricScoringModel.WeightedPercentage ? $"{row.TotalMarks:G29}% total weight" :
            row.ScoringModel == RubricScoringModel.Checklist && !row.HasPerformanceLevels ? $"{row.CriteriaCount} checklist items" : $"{row.TotalMarks:G29} points max",
        "usageCount" => $"{row.UsageCount} times", "criteria" => row.CriteriaCount.ToString(CultureInfo.InvariantCulture),
        "lastModificationTime" => Local(row.LastModificationTime ?? row.CreationTime, zone).ToString("dd-MM-yyyy", CultureInfo.InvariantCulture),
        "status" => row.Status.ToString(), _ => string.Empty
    };

    private static DateTime Local(DateTime value, TimeZoneInfo zone) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(value, DateTimeKind.Utc), zone);
    private static IEnumerable<string> SearchText(RubricTemplateSummaryDto row, string column, TimeZoneInfo zone)
    {
        yield return Display(row, column, zone);
        yield return Convert.ToString(Value(row, column), CultureInfo.InvariantCulture) ?? string.Empty;
        if (column != "lastModificationTime" || !row.LastModificationTime.HasValue) yield break;
        yield return row.LastModificationTime.Value.ToString("O", CultureInfo.InvariantCulture);
        yield return Local(row.LastModificationTime.Value, zone).ToString("hh:mm tt", CultureInfo.InvariantCulture);
        yield return Local(row.LastModificationTime.Value, zone).ToString("h:mm tt", CultureInfo.InvariantCulture);
    }
    private static bool Number(JsonElement value, out double number) => double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number);
    private static int Compare(object? first, object? second) => first == null ? second == null ? 0 : -1 : second == null ? 1 :
        first is string text ? StringComparer.Ordinal.Compare(text, (string)second) : ((IComparable)first).CompareTo(second);
    private static List<T> Parse<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? []; }
        catch (JsonException) { throw new BusinessException("AssessmentRubric:InvalidListCriteria"); }
    }
    private sealed class TemplateFilter
    {
        public TemplateFilter() { }
        public string Column { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string ValueType { get; set; } = string.Empty;
        public JsonElement Value { get; set; }
    }
    private sealed class TemplateSort
    {
        public TemplateSort() { }
        public string Column { get; set; } = string.Empty;
        public string Direction { get; set; } = "asc";
    }
}
