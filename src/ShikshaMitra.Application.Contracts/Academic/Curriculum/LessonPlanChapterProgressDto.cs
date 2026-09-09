using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.Curriculum;

public class GetLessonPlanChapterProgressInput
{
    [Required]
    public Guid AcademicYearId { get; set; }
    [Required]
    public Guid StandardId { get; set; }
    [Required]
    public Guid DivisionId { get; set; }
    [Required]
    public Guid SubjectId { get; set; }
    [Required]
    public Guid ChapterId { get; set; }
}

public class LessonPlanChapterProgressDto
{
    public int MinimumCompletionPercentage { get; set; }
}
