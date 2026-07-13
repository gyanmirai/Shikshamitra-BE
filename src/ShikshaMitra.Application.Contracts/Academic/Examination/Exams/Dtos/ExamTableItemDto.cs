using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Examination.Exams.Dtos;

public class ExamListResultDto : PagedResultDto<ExamTableItemDto>
{
    public int PendingCount { get; set; }
    public int PublishedCount { get; set; }
    public int CompletedCount { get; set; }
    public int UpcomingCount { get; set; }
    public int UnassignedRoomsCount { get; set; }
    public int NeedsAttentionCount { get; set; }
}

public class ExamTableItemDto
{
    public Guid ExamId { get; set; }
    public string ExamName { get; set; }
    public string ExamTypeName { get; set; }
    public string StandardName { get; set; }
    public List<string> Divisions { get; set; } = new();
    public Guid? CreatedBy { get; set; }
    public string? CreatorName { get; set; }
    public ExamStatus Status { get; set; }
    public int SubjectCount { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsScheduleComplete { get; set; }
    public bool HasRoomAssignment { get; set; }
    public int MissingRoomAssignmentCount { get; set; }
    public int IssueCount { get; set; }
    public int WarningCount { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}
