using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;

public class SeatingPlanSubjectDto
{
    public Guid ExamSubjectId { get; set; }
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public DateTime? ExamDate { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}

public class SeatingPlanScopeDto
{
    public Guid SeatingPlanId { get; set; }
    public Guid ExamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public SeatingPlanScopeType ScopeType { get; set; }
    public SeatingPlanStatus Status { get; set; }
    public bool IsLocked { get; set; }
    public List<SeatingPlanSubjectDto> Subjects { get; set; } = [];
    public int TotalStudents { get; set; }
    public int AllocatedStudents { get; set; }
    public int PendingStudents { get; set; }
    public int AllocationProgress { get; set; }
}

public class SeatingPlanScopeListResponseDto
{
    public Guid ExamId { get; set; }
    public List<SeatingPlanSubjectDto> AvailableSubjects { get; set; } = [];
    public List<SeatingPlanScopeDto> Items { get; set; } = [];
}

public class SaveSeatingPlanScopeInputDto
{
    public Guid? SeatingPlanId { get; set; }
    public string Name { get; set; } = string.Empty;
    public SeatingPlanScopeType ScopeType { get; set; }
    public List<Guid> ExamSubjectIds { get; set; } = [];
}
