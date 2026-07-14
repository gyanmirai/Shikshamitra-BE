using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.Curriculum;

public class UpdateLessonPlanDto
{
    [Required]
    public Guid ChapterId { get; set; }

    [Required]
    [MaxLength(256)]
    public string Topic { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string? LearningObjective { get; set; }

    [MaxLength(256)]
    public string? TeachingMethod { get; set; }

    [MaxLength(512)]
    public string? ResourcesRequired { get; set; }

    [Range(0, 100)]
    public int PlannedCompletionPercentage { get; set; }
}
