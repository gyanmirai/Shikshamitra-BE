using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Curriculum;

public class TeacherDiary : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid LessonPlanId { get; private set; }
    public Guid TeacherId { get; private set; }
    public DateTime ActualDate { get; private set; }
    public bool ClassConducted { get; private set; }
    public string? ActualTopicCovered { get; private set; }
    public string? ActualContentSummary { get; private set; }
    public int ActualCompletionPercentage { get; private set; }
    public string? TeachingMethodUsed { get; private set; }
    public string? StudentResponse { get; private set; }
    public string? ClassUnderstandingLevel { get; private set; }
    public string? StudentEngagementLevel { get; private set; }
    public bool NeedRevision { get; private set; }
    public string? Reflection { get; private set; }
    public string? WhatWentWell { get; private set; }
    public string? WhatWasDifficult { get; private set; }
    public string? StudentsNeedingSupport { get; private set; }
    public string? NextClassFollowUp { get; private set; }
    public string? RemedialActionRequired { get; private set; }
    public string? HomeworkAssigned { get; private set; }
    public string? MissedReason { get; private set; }
    public DateTime? RescheduledToDate { get; private set; }
    public bool IsBackdated { get; private set; }
    public string? BackdatedReason { get; private set; }
    public string? TeacherRemarks { get; private set; }
    public TeacherDiaryStatus Status { get; private set; }
    public DateTime? SubmittedAt { get; private set; }

    protected TeacherDiary() { }

    public TeacherDiary(Guid id, Guid? tenantId, Guid lessonPlanId, Guid teacherId, DateTime actualDate) : base(id)
    {
        TenantId = tenantId;
        LessonPlanId = lessonPlanId;
        TeacherId = teacherId;
        ActualDate = actualDate;
        ClassConducted = true;
        Status = TeacherDiaryStatus.Draft;
    }

    public void Update(
        DateTime actualDate,
        bool classConducted,
        string? actualTopicCovered,
        string? actualContentSummary,
        int actualCompletionPercentage,
        string? teachingMethodUsed,
        string? studentResponse,
        string? classUnderstandingLevel,
        string? studentEngagementLevel,
        bool needRevision,
        string? reflection,
        string? whatWentWell,
        string? whatWasDifficult,
        string? studentsNeedingSupport,
        string? nextClassFollowUp,
        string? remedialActionRequired,
        string? homeworkAssigned,
        string? missedReason,
        DateTime? rescheduledToDate,
        bool isBackdated,
        string? backdatedReason,
        string? teacherRemarks)
    {
        ActualDate = actualDate;
        ClassConducted = classConducted;
        ActualTopicCovered = actualTopicCovered;
        ActualContentSummary = actualContentSummary;
        ActualCompletionPercentage = Math.Clamp(actualCompletionPercentage, 0, 100);
        TeachingMethodUsed = teachingMethodUsed;
        StudentResponse = studentResponse;
        ClassUnderstandingLevel = classUnderstandingLevel;
        StudentEngagementLevel = studentEngagementLevel;
        NeedRevision = needRevision;
        Reflection = reflection;
        WhatWentWell = whatWentWell;
        WhatWasDifficult = whatWasDifficult;
        StudentsNeedingSupport = studentsNeedingSupport;
        NextClassFollowUp = nextClassFollowUp;
        RemedialActionRequired = remedialActionRequired;
        HomeworkAssigned = homeworkAssigned;
        MissedReason = missedReason;
        RescheduledToDate = rescheduledToDate;
        IsBackdated = isBackdated;
        BackdatedReason = backdatedReason;
        TeacherRemarks = teacherRemarks;
    }

    public void Submit()
    {
        Status = TeacherDiaryStatus.Submitted;
        SubmittedAt ??= DateTime.UtcNow;
    }
}
