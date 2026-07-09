using System;

namespace ShikshaMitra.Academic.Curriculum;

public class TimelineSlotDto
{
    public Guid TimetableSlotId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public Guid StandardId { get; set; }
    public string StandardName { get; set; } = string.Empty;
    public Guid DivisionId { get; set; }
    public string DivisionName { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    
    public LessonPlanDto? LessonPlan { get; set; }
    public bool IsTimetableSlotOnly { get; set; }
    public string? InsightType { get; set; }
    public string? InsightSeverity { get; set; }
    public string? InsightMessage { get; set; }
    public bool IsPlanCreationBlocked { get; set; }
}
