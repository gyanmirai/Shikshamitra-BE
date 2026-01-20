using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.Examination.Exams.Dtos;

public class ExamBasicDetailsRequestDto
{
    [Required]
    public Guid ExamTypeId { get; set; }

    [Required]
    public string ExamName { get; set; } = null!;

    [Required]
    public Guid StandardId { get; set; }

    [Required]
    [MinLength(1)]
    public List<Guid> DivisionIds { get; set; } = new();

    [Required]
    [MinLength(1)]
    public List<Guid> SubjectIds { get; set; } = new();
}
