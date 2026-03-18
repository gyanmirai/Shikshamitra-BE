using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.Examination.Settings;

public class UpdateExamTypeDto
{
    [Required]
    public string ExamType { get; set; } = null!;

    public string? Description { get; set; }
}
