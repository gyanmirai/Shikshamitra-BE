using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class AssignmentV2Dto
{
    public Guid Id { get; set; }
    public Guid? AcademicYearId { get; set; }
    public Guid AcademicMediumId { get; set; }
    public Guid StandardId { get; set; }
    public string StandardName { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Guid AssignmentTypeId { get; set; }
    public string AssignmentTypeName { get; set; } = string.Empty;
    public int? DefaultSubmissionType { get; set; }
    public decimal? DefaultMarks { get; set; }
    public int? DefaultDueDays { get; set; }
    public bool EvaluationRequired { get; set; } = true;
    public int? EvaluationMethod { get; set; }
    public int? DefaultEvaluationMethod { get; set; }
    public int? AssignmentTypeStatus { get; set; }
    public decimal TotalMarks { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Attachment { get; set; }
    public List<Guid> ChapterIds { get; set; } = new();
    public int SubmissionType { get; set; }
    public int Status { get; set; }
    public List<AssignmentV2DivisionScheduleDto> DivisionSchedules { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
}

public class AssignmentV2DivisionScheduleDto
{
    public Guid DivisionId { get; set; }
    public string DivisionName { get; set; } = string.Empty;
    public DateTime? PublishAt { get; set; }
    public DateTime DueAt { get; set; }
    public bool IsPublished { get; set; }
}
