using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class HomeworkDto
{
    public Guid Id { get; set; }
    public Guid AcademicMediumId { get; set; }
    public Guid StandardId { get; set; }
    public string StandardName { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public Guid? ChapterId { get; set; }
    public string? ChapterName { get; set; }
    public List<Guid>? ChapterIds { get; set; } = new();
    public List<string>? ChapterNames { get; set; } = new();
    public string Title { get; set; } = string.Empty;
    public Guid HomeworkTypeId { get; set; }
    public string HomeworkTypeName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Attachment { get; set; }
    public int SubmissionType { get; set; }
    public int Status { get; set; }
    public List<HomeworkDivisionScheduleDto> DivisionSchedules { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
}

public class HomeworkDivisionScheduleDto
{
    public Guid DivisionId { get; set; }
    public string DivisionName { get; set; } = string.Empty;
    public DateTime? PublishAt { get; set; }
    public DateTime DueAt { get; set; }
    public bool IsPublished { get; set; }
}
