using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Curriculum;

public class GetTeacherDiariesInput : PagedAndSortedResultRequestDto
{
    public Guid? LessonPlanId { get; set; }
    public Guid? AcademicYearId { get; set; }
    public Guid? TeacherId { get; set; }
    public Guid? StandardId { get; set; }
    public Guid? DivisionId { get; set; }
    public Guid? SubjectId { get; set; }
    public Guid? ChapterId { get; set; }
    public TeacherDiaryStatus? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class TeacherDiaryDto : FullAuditedEntityDto<Guid>
{
    public Guid LessonPlanId { get; set; }
    public Guid TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public DateTime ActualDate { get; set; }
    public bool ClassConducted { get; set; }
    public string? ActualTopicCovered { get; set; }
    public string? ActualTopicTaught { get; set; }
    public string? ActualContentSummary { get; set; }
    public int ActualCompletionPercentage { get; set; }
    public int CompletionPercentage { get; set; }
    public string? TeachingMethodUsed { get; set; }
    public string? StudentResponse { get; set; }
    public string? ClassUnderstandingLevel { get; set; }
    public string? StudentEngagementLevel { get; set; }
    public bool NeedRevision { get; set; }
    public string? Reflection { get; set; }
    public string? WhatWentWell { get; set; }
    public string? WhatWasDifficult { get; set; }
    public string? StudentsNeedingSupport { get; set; }
    public string? NextClassFollowUp { get; set; }
    public string? RemedialActionRequired { get; set; }
    public string? HomeworkAssigned { get; set; }
    public string? MissedReason { get; set; }
    public DateTime? RescheduledToDate { get; set; }
    public bool IsBackdated { get; set; }
    public string? BackdatedReason { get; set; }
    public bool IsSubstituteDiary { get; set; }
    public string? TeacherRemarks { get; set; }
    public string? ResourcesUsed { get; set; }
    public TeacherDiaryStatus Status { get; set; }
    public TeacherDiaryStatus DiaryStatus { get; set; }
    public DateTime? SubmittedAt { get; set; }
}

public class CreateUpdateTeacherDiaryDto
{
    [Required]
    public Guid LessonPlanId { get; set; }

    [Required]
    public DateTime ActualDate { get; set; }

    public bool ClassConducted { get; set; } = true;

    [MaxLength(256)]
    public string? ActualTopicCovered { get; set; }

    [MaxLength(2048)]
    public string? ActualContentSummary { get; set; }

    [Range(0, 100)]
    public int ActualCompletionPercentage { get; set; }

    [MaxLength(256)]
    public string? TeachingMethodUsed { get; set; }

    [MaxLength(1024)]
    public string? StudentResponse { get; set; }

    [MaxLength(64)]
    public string? ClassUnderstandingLevel { get; set; }

    [MaxLength(64)]
    public string? StudentEngagementLevel { get; set; }

    public bool NeedRevision { get; set; }

    [MaxLength(2048)]
    public string? Reflection { get; set; }

    [MaxLength(1024)]
    public string? WhatWentWell { get; set; }

    [MaxLength(1024)]
    public string? WhatWasDifficult { get; set; }

    [MaxLength(1024)]
    public string? StudentsNeedingSupport { get; set; }

    [MaxLength(1024)]
    public string? NextClassFollowUp { get; set; }

    [MaxLength(1024)]
    public string? RemedialActionRequired { get; set; }

    [MaxLength(1024)]
    public string? HomeworkAssigned { get; set; }

    [MaxLength(128)]
    public string? MissedReason { get; set; }

    public DateTime? RescheduledToDate { get; set; }
    public bool IsBackdated { get; set; }

    [MaxLength(1024)]
    public string? BackdatedReason { get; set; }

    [MaxLength(2048)]
    public string? TeacherRemarks { get; set; }
}
