using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Examination.Exams.Dtos;

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
}
