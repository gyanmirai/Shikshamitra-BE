using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.Curriculum;

public class CreateHomeworkFromLessonPlanDto
{
    [Required]
    public Guid AcademicMediumId { get; set; }

    [Required]
    public Guid HomeworkTypeId { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Range(0, 2)]
    public int SubmissionType { get; set; }

    public DateTime? PublishAt { get; set; }

    [Required]
    public DateTime DueAt { get; set; }
}
