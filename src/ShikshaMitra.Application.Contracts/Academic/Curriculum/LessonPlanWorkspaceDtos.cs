using System.Collections.Generic;

namespace ShikshaMitra.Academic.Curriculum;

public class GetLessonPlanWorkspaceInput : GetLessonPlansInput
{
    public string View { get; set; } = "lessonList";
    public string? UiCriteria { get; set; }
    public string? SearchTerm { get; set; }
    public string[]? SearchColumns { get; set; }
    public string? Filters { get; set; }
    public string? Sorts { get; set; }
}

public class LessonPlanWorkspaceDto
{
    public long TotalCount { get; set; }
    public List<LessonPlanDto> Items { get; set; } = new();
    public List<TeacherDiaryDto> Diaries { get; set; } = new();
    public LessonPlanWorkspaceSummaryDto Summary { get; set; } = new();
}

public class LessonPlanWorkspaceSummaryDto
{
    public Dictionary<string, int> ReviewStatusCounts { get; set; } = new();
    public Dictionary<string, int> QuickCounts { get; set; } = new();
    public Dictionary<string, string[]> Options { get; set; } = new();
}
