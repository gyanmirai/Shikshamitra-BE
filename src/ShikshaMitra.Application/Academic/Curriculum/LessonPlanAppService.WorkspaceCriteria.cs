using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Academic.Curriculum;

public partial class LessonPlanAppService
{
    private static T ParseWorkspaceJson<T>(string? json) where T : new()
    {
        try { return string.IsNullOrWhiteSpace(json) ? new T() : JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new T(); }
        catch (JsonException) { throw new BusinessException("LessonPlan:InvalidWorkspaceCriteria"); }
    }

    private static string WorkspaceText(Dictionary<string, JsonElement> criteria, string key) =>
        criteria.TryGetValue(key, out var value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined) ? value.ToString() : "";

    private static bool MatchesWorkspaceUi(WorkspaceKey row, GetLessonPlanWorkspaceInput input, Dictionary<string, JsonElement> criteria)
    {
        bool Equal(string property, string value) { var expected = WorkspaceText(criteria, property); return expected is "" or "all" || expected == value; }
        bool DateRange(string fromKey, string toKey) =>
            (WorkspaceText(criteria, fromKey) is "" || string.CompareOrdinal(row.Date, WorkspaceText(criteria, fromKey)) >= 0) &&
            (WorkspaceText(criteria, toKey) is "" || string.CompareOrdinal(row.Date, WorkspaceText(criteria, toKey)) <= 0);
        var today = WorkspaceText(criteria, "todayIso");
        if (input.View == "teacherDiary") return string.IsNullOrEmpty(today) || string.CompareOrdinal(row.Date, today) <= 0;
        if (input.View == "reviewQueue")
        {
            if (row.Status is not (LessonPlanStatus.Submitted or LessonPlanStatus.Approved or LessonPlanStatus.Rejected or LessonPlanStatus.NeedsChanges)) return false;
            if (!Equal("selectedClass", row.ClassDivision) || !Equal("selectedSubject", row.SubjectName) || !Equal("selectedReviewer", row.ReviewerName)) return false;
            return WorkspaceText(criteria, "dateFilterMode") switch
            {
                "day" => Equal("filterDate", row.Date),
                "week" or "range" => DateRange("rangeStart", "rangeEnd"),
                _ => true
            };
        }
        if (!Equal("academicYearFilter", "2026-27") || !Equal("teacherFilter", row.TeacherName) || !Equal("classFilter", row.ClassName) ||
            !Equal("divisionFilter", row.DivisionName) || !Equal("subjectFilter", row.SubjectName) || !Equal("chapterFilter", row.ChapterName) ||
            !Equal("planStatusFilter", row.Status.ToString()) || !Equal("diaryStatusFilter", row.DiaryStatus) ||
            !Equal("approvalStatusFilter", row.Status == LessonPlanStatus.Approved ? "Approved" : "Pending") || !DateRange("fromDate", "toDate")) return false;
        var week = WorkspaceText(criteria, "weekStartIso");
        return WorkspaceText(criteria, "quickFilter") switch
        {
            "today" => row.Date == today,
            "this_week" => DateTime.TryParse(week, out var start) && string.CompareOrdinal(row.Date, week) >= 0 && string.CompareOrdinal(row.Date, start.AddDays(6).ToString("yyyy-MM-dd")) <= 0,
            "pending_diary" => row.DiaryStatus == "NotSubmitted",
            "pending_approval" => row.Status == LessonPlanStatus.Submitted,
            "missed_lessons" => row.Status.ToString() == "Missed",
            "delayed_progress" => row.PlannedPercentage - row.ActualPercentage > 10,
            "backdated" => row.Diaries.FirstOrDefault()?.IsBackdated == true,
            _ => true
        };
    }

    private static readonly string[] PlanWorkspaceColumns = { "plannedDate", "period", "plannedTime", "className", "subjectName", "chapterName", "topic", "teacherName", "insightMessage", "plannedCompletionPercentage", "actualCompletionPercentage", "planStatus", "diaryStatus" };
    private static readonly string[] DiaryWorkspaceColumns = { "plannedDate", "period", "className", "subjectName", "chapterName", "topic", "teacherName", "actualCompletionPercentage", "diaryStatus" };
    private static readonly string[] ReviewWorkspaceColumns = { "teacherName", "submittedDate", "pendingSince", "classDivision", "subjectName", "chapterName", "topic", "plannedPct", "actualPct", "status" };

    private static bool WorkspaceNeedsInsights(GetLessonPlanWorkspaceInput input, List<WorkspaceFilter> filters, List<WorkspaceSort> sorts) =>
        (!string.IsNullOrEmpty(input.SearchTerm) && input.View == "lessonList" && (input.SearchColumns == null || input.SearchColumns.Contains("insightMessage"))) ||
        filters.Any(filter => filter.Column == "insightMessage") || sorts.Any(sort => sort.Column == "insightMessage");

    private static IEnumerable<WorkspaceKey> ApplyWorkspaceTable(IEnumerable<WorkspaceKey> rows, GetLessonPlanWorkspaceInput input, List<WorkspaceFilter> filters, List<WorkspaceSort> sorts)
    {
        var columns = input.View == "reviewQueue" ? ReviewWorkspaceColumns : input.View == "teacherDiary" ? DiaryWorkspaceColumns : PlanWorkspaceColumns;
        if (!string.IsNullOrEmpty(input.SearchTerm))
        {
            var searchable = input.SearchColumns == null ? columns : input.SearchColumns.Where(columns.Contains).ToArray();
            rows = rows.Where(row => searchable.Any(column => WorkspaceString(WorkspaceValue(row, column, input.View)).Contains(input.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                WorkspaceDisplay(row, column, input.View).Contains(input.SearchTerm, StringComparison.OrdinalIgnoreCase)));
        }
        foreach (var group in filters.GroupBy(filter => filter.Column))
            rows = rows.Where(row => group.Any(filter => MatchesWorkspaceFilter(row, filter, input.View)));
        IOrderedEnumerable<WorkspaceKey>? ordered = null;
        foreach (var sort in sorts.Where(sort => columns.Contains(sort.Column)))
        {
            Func<WorkspaceKey, object?> value = row => WorkspaceValue(row, sort.Column, input.View);
            ordered = ordered == null ? sort.Direction == "desc" ? rows.OrderByDescending(value, WorkspaceValueComparer.Instance) : rows.OrderBy(value, WorkspaceValueComparer.Instance)
                : sort.Direction == "desc" ? ordered.ThenByDescending(value, WorkspaceValueComparer.Instance) : ordered.ThenBy(value, WorkspaceValueComparer.Instance);
        }
        return ordered ?? rows;
    }

    private static object? WorkspaceValue(WorkspaceKey row, string column, string view) => column switch
    {
        "plannedDate" or "submittedDate" or "pendingSince" => row.Date,
        "period" => row.Period, "plannedTime" => row.PlannedTime, "className" => row.ClassName, "classDivision" => row.ClassDivision,
        "subjectName" => row.SubjectName, "chapterName" => row.ChapterName, "topic" => row.Topic, "teacherName" => row.TeacherName,
        "insightMessage" => row.InsightMessage, "plannedCompletionPercentage" or "plannedPct" => row.PlannedPercentage,
        "actualCompletionPercentage" or "actualPct" => row.ActualPercentage, "diaryStatus" => row.DiaryStatus,
        "planStatus" => row.Status.ToString(), "status" => view == "reviewQueue" ? row.ReviewStatus : row.Status.ToString(), _ => null
    };

    private static string WorkspaceString(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    private static string WorkspaceDisplay(WorkspaceKey row, string column, string view) => column switch
    {
        "plannedDate" or "submittedDate" or "pendingSince" => DateTime.Parse(row.Date, CultureInfo.InvariantCulture).ToString("dd-MM-yyyy"),
        "className" => row.ClassDivision,
        "plannedCompletionPercentage" or "plannedPct" => $"{row.PlannedPercentage}%",
        "actualCompletionPercentage" => $"{row.ActualPercentage}%",
        "actualPct" => $"{row.ActualPercentage}%", "diaryStatus" => row.DiaryStatus == "NotSubmitted" ? "Pending" : row.DiaryStatus,
        "status" when view == "reviewQueue" => row.ReviewStatus == "changes" ? "Changes Requested" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(row.ReviewStatus),
        "insightMessage" => row.InsightMessage ?? "-", _ => WorkspaceString(WorkspaceValue(row, column, view))
    };

    private static bool MatchesWorkspaceFilter(WorkspaceKey row, WorkspaceFilter filter, string view)
    {
        var raw = WorkspaceValue(row, filter.Column, view);
        var expected = filter.Value.ToString();
        if (filter.ValueType == "enum") return WorkspaceString(raw) == expected;
        if (filter.ValueType is "date" or "dateTime")
        {
            if (!TryWorkspaceDate(WorkspaceString(raw), out var date) || !TryWorkspaceDate(expected, out var target)) return false;
            return filter.Type switch { "equals" => date.Date == target.Date, "after" => date.Date >= target.Date, "before" => date.Date <= target.Date, _ => false };
        }
        if (filter.ValueType is "number" or "formattedNumber")
        {
            if (!decimal.TryParse(WorkspaceString(raw), NumberStyles.Number, CultureInfo.InvariantCulture, out var number)) return false;
            if (filter.Type == "between") return filter.Value.ValueKind == JsonValueKind.Object &&
                decimal.TryParse(filter.Value.GetProperty("min").ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var min) &&
                decimal.TryParse(filter.Value.GetProperty("max").ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var max) && number >= min && number <= max;
            if (!decimal.TryParse(expected, NumberStyles.Number, CultureInfo.InvariantCulture, out var target)) return false;
            return filter.Type switch { "equals" => number == target, "greaterThan" => number > target, "lessThan" => number < target, _ => false };
        }
        var text = WorkspaceDisplay(row, filter.Column, view);
        return filter.Type switch { "equals" => text.Equals(expected, StringComparison.OrdinalIgnoreCase), "contains" => text.Contains(expected, StringComparison.OrdinalIgnoreCase),
            "startsWith" => text.StartsWith(expected, StringComparison.OrdinalIgnoreCase), "endsWith" => text.EndsWith(expected, StringComparison.OrdinalIgnoreCase), _ => true };
    }

    private static bool TryWorkspaceDate(string value, out DateTime date) =>
        DateTime.TryParseExact(value, new[] { "dd-MM-yyyy", "yyyy-MM-dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out date) ||
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out date);

    private sealed class WorkspaceFilter
    {
        public string Column { get; set; } = "";
        public string Type { get; set; } = "contains";
        public JsonElement Value { get; set; }
        public string ValueType { get; set; } = "string";
    }
    private sealed class WorkspaceSort
    {
        public string Column { get; set; } = "";
        public string Direction { get; set; } = "asc";
    }
    private sealed class WorkspaceValueComparer : IComparer<object?>
    {
        public static WorkspaceValueComparer Instance { get; } = new();
        public int Compare(object? left, object? right) => left is int a && right is int b ? a.CompareTo(b) :
            left == null ? right == null ? 0 : 1 : right == null ? -1 : StringComparer.Ordinal.Compare(WorkspaceString(left), WorkspaceString(right));
    }
}
