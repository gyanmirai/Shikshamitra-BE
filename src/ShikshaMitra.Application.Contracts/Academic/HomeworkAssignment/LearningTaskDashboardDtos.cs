using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class LearningTaskDashboardFilterDto
{
    public Guid? StandardId { get; set; }
    public Guid? DivisionId { get; set; }
    public Guid? SubjectId { get; set; }
    public DateTime? AsOfDate { get; set; }
}

public class LearningTaskDashboardDto
{
    public int TotalTasks { get; set; }
    public int PendingPublishCount { get; set; }
    public int DueTodayCount { get; set; }
    public int OverdueCount { get; set; }
    public int EvaluationPendingCount { get; set; }
    public List<LearningTaskDashboardItemDto> PendingPublish { get; set; } = [];
    public List<LearningTaskDashboardItemDto> DueToday { get; set; } = [];
    public List<LearningTaskDashboardItemDto> Overdue { get; set; } = [];
    public List<LearningTaskDashboardItemDto> EvaluationPending { get; set; } = [];
}

public class LearningTaskDashboardItemDto
{
    public Guid Id { get; set; }
    public string TaskType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Guid StandardId { get; set; }
    public string StandardName { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public int Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public DateTime? NextPublishAt { get; set; }
    public DateTime? NextDueAt { get; set; }
    public int PublishedDivisionCount { get; set; }
    public int TotalDivisionCount { get; set; }
    public int SubmittedCount { get; set; }
    public int LateCount { get; set; }
    public int EvaluationPendingCount { get; set; }
    public List<Guid> DivisionIds { get; set; } = [];
    public List<string> DivisionNames { get; set; } = [];
}

public class LearningTaskReminderRequestDto
{
    public Guid? DivisionId { get; set; }
    public string? Message { get; set; }
}

public class LearningTaskReminderResponseDto
{
    public Guid TaskId { get; set; }
    public string TaskType { get; set; } = string.Empty;
    public int TargetDivisionCount { get; set; }
    public int PendingSubmissionCount { get; set; }
    public DateTime ReminderQueuedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class LearningTaskHistoryDto
{
    public Guid TaskId { get; set; }
    public string TaskType { get; set; } = string.Empty;
    public List<LearningTaskHistoryItemDto> Items { get; set; } = [];
}

public class LearningTaskHistoryItemDto
{
    public string Action { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public Guid? ActorUserId { get; set; }
    public string Severity { get; set; } = "info";
}
