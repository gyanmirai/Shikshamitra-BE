using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;

public class ExaminationSeatingReadinessDto
{
    public Guid ExamId { get; set; }
    public bool IsPublished { get; set; }
    public bool IsReadyToPublish { get; set; }
    public bool CanResetDraft { get; set; }
    public int TotalSubjectCount { get; set; }
    public int PlannedSubjectCount { get; set; }
    public int TotalPlanCount { get; set; }
    public int ReadyPlanCount { get; set; }
    public int TotalStudentSeats { get; set; }
    public int AllocatedStudentSeats { get; set; }
    public DateTime? PublishedAt { get; set; }
    public Guid? PublishedByUserId { get; set; }
    public string? PublicationVersion { get; set; }
    public List<ExaminationSeatingIssueDto> Blockers { get; set; } = [];
    public List<ExaminationSeatingIssueDto> Warnings { get; set; } = [];
}

public class ExaminationSeatingIssueDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? SeatingPlanId { get; set; }
    public string? SeatingPlanName { get; set; }
    public List<Guid> ExamSubjectIds { get; set; } = [];
}

public class ResetExaminationSeatingDraftInputDto
{
    public string Reason { get; set; } = string.Empty;
}
