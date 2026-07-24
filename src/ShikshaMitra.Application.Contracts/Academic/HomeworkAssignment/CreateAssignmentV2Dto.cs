using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class CreateAssignmentV2Dto
{
    [Required]
    public Guid StandardId { get; set; }

    [Required]
    public Guid SubjectId { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public Guid AssignmentTypeId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal TotalMarks { get; set; }

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Range(0, 2)]
    public int SubmissionType { get; set; }

    /// <summary>
    /// Optional chapter IDs to link this assignment to the Syllabus Assessment status.
    /// </summary>
    public List<Guid>? ChapterIds { get; set; }

    [Required]
    [MinLength(1)]
    public List<CreateAssignmentV2DivisionScheduleDto> DivisionSchedules { get; set; } = new();
}

public class CreateAssignmentV2DivisionScheduleDto
{
    [Required]
    public Guid DivisionId { get; set; }

    public DateTime? PublishAt { get; set; }

    [Required]
    public DateTime DueAt { get; set; }
}
