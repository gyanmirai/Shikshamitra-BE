using System;

namespace ShikshaMitra.Academic.Examination.Settings;

public class ExamTypeDto
{
    public Guid Id { get; set; }
    public string ExamType { get; set; } = null!;
    public string? Description { get; set; }
    public Guid AcademicMediumId { get; set; }
    public DateTime CreatedAt { get; set; }
}
