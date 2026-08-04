using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.Examination.Settings;

public class UpdateExamTypeDto
{
    [Required]
    public string ExamType { get; set; } = null!;

    public string? Description { get; set; }

    public string? Category { get; set; }

    public SettingStatus Status { get; set; }

    public double? DefaultTotalMarks { get; set; }

    public double? DefaultWeightage { get; set; }

    public Guid? DefaultGradeScaleId { get; set; }
}
