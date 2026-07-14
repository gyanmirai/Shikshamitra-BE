using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Curriculum;

public class LessonPlanDto : FullAuditedEntityDto<Guid>
{
    public Guid AcademicYearId { get; set; }
    public Guid TeacherId { get; set; }
    public Guid StandardId { get; set; }
    public Guid DivisionId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid ChapterId { get; set; }
    public DateTime PlannedDate { get; set; }
    public Guid? TimetableSlotId { get; set; }
    public string? PeriodName { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? SlotStartTime { get; set; }
    public string? SlotEndTime { get; set; }
    public string? TeacherName { get; set; }
    public string? StandardName { get; set; }
    public string? DivisionName { get; set; }
    public string? SubjectName { get; set; }
    public string? ChapterName { get; set; }

    public string Topic { get; set; } = string.Empty;
    public string? LearningObjective { get; set; }
    public string? TeachingMethod { get; set; }
    public string? ResourcesRequired { get; set; }
    public int PlannedCompletionPercentage { get; set; }

    public LessonPlanStatus Status { get; set; }
    public string? ReviewRemarks { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public bool IsAiGenerated { get; set; }
    public TeacherDiaryStatus DiaryStatus { get; set; }
    public int ActualCompletionPercentage { get; set; }
    public Guid? TeacherDiaryId { get; set; }
    public string? InsightType { get; set; }
    public string? InsightSeverity { get; set; }
    public string? InsightMessage { get; set; }
    public bool IsPlanCreationBlocked { get; set; }
}
