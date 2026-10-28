using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.Examination.Exams.Dtos;

public class GetExamGradeOptionsInput
{
    [Required]
    public Guid ExamId { get; set; }
    public Guid? SubjectId { get; set; }
    public Guid? DivisionId { get; set; }
    public Guid? StandardId { get; set; }
}
