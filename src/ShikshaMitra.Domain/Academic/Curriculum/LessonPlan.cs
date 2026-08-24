using System;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Curriculum;

public class LessonPlan : FullAuditedAggregateRoot<Guid>, IMultiTenant, IUserScopedEntity
{
    public Guid? TenantId { get; private set; }
    public Guid AcademicYearId { get; private set; }
    public Guid TeacherId { get; private set; }
    public Guid StandardId { get; private set; }
    public Guid DivisionId { get; private set; }
    public Guid SubjectId { get; private set; }
    public Guid ChapterId { get; private set; }
    public DateTime PlannedDate { get; private set; }
    public Guid? TimetableSlotId { get; private set; } // Links to timetableslot if scheduled

    public string Topic { get; private set; }
    public string? LearningObjective { get; private set; }
    public string? TeachingMethod { get; private set; }
    public string? ResourcesRequired { get; private set; }
    public int PlannedCompletionPercentage { get; private set; }

    public LessonPlanStatus Status { get; private set; }
    public string? ReviewRemarks { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public bool IsAiGenerated { get; private set; }
    public TeacherDiary? TeacherDiary { get; private set; }

    Guid? IUserScopedEntity.AcademicYearId => AcademicYearId;
    Guid? IUserScopedEntity.EducationBoardId => null;
    Guid? IUserScopedEntity.EducationMediumId => null;
    Guid? IUserScopedEntity.EducationMediumStageId => null;
    Guid? IUserScopedEntity.StandardId => StandardId;
    Guid? IUserScopedEntity.DivisionId => DivisionId;

    protected LessonPlan() { }

    public LessonPlan(
        Guid id,
        Guid? tenantId,
        Guid academicYearId,
        Guid teacherId,
        Guid standardId,
        Guid divisionId,
        Guid subjectId,
        Guid chapterId,
        DateTime plannedDate,
        string topic,
        int plannedCompletionPercentage,
        Guid? timetableSlotId = null,
        bool isAiGenerated = false) : base(id)
    {
        TenantId = tenantId;
        AcademicYearId = academicYearId;
        TeacherId = teacherId;
        StandardId = standardId;
        DivisionId = divisionId;
        SubjectId = subjectId;
        ChapterId = chapterId;
        PlannedDate = plannedDate;
        Topic = topic;
        PlannedCompletionPercentage = Math.Clamp(plannedCompletionPercentage, 0, 100);
        TimetableSlotId = timetableSlotId;
        Status = LessonPlanStatus.Draft;
        IsAiGenerated = isAiGenerated;
    }

    public void UpdatePlan(
        Guid chapterId,
        string topic,
        string? learningObjective,
        string? teachingMethod,
        string? resourcesRequired,
        int plannedCompletionPercentage)
    {
        if (Status == LessonPlanStatus.Approved)
        {
            throw new InvalidOperationException("Approved lesson plans cannot be modified.");
        }

        ChapterId = chapterId;
        Topic = topic;
        LearningObjective = learningObjective;
        TeachingMethod = teachingMethod;
        ResourcesRequired = resourcesRequired;
        PlannedCompletionPercentage = Math.Clamp(plannedCompletionPercentage, 0, 100);
    }

    public void Submit()
    {
        Status = LessonPlanStatus.Submitted;
    }

    public void Approve(Guid reviewerId, string? remarks)
    {
        Status = LessonPlanStatus.Approved;
        ReviewedBy = reviewerId;
        ReviewedAt = DateTime.UtcNow;
        ReviewRemarks = remarks;
    }

    public void Reject(Guid reviewerId, string? remarks)
    {
        Status = LessonPlanStatus.Rejected;
        ReviewedBy = reviewerId;
        ReviewedAt = DateTime.UtcNow;
        ReviewRemarks = remarks;
    }

    public void RequestChanges(Guid reviewerId, string? remarks)
    {
        Status = LessonPlanStatus.NeedsChanges;
        ReviewedBy = reviewerId;
        ReviewedAt = DateTime.UtcNow;
        ReviewRemarks = remarks;
    }
}
