using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class CreateHomeworkDto
{
    [Required]
    public Guid StandardId { get; set; }

    [Required]
    public Guid SubjectId { get; set; }

    [Required]
    public Guid ChapterId { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public Guid HomeworkTypeId { get; set; }

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Range(0, 2)]
    public int SubmissionType { get; set; }

    [Required]
    [MinLength(1)]
    public List<CreateHomeworkDivisionScheduleDto> DivisionSchedules { get; set; } = new();
}

public class CreateHomeworkDivisionScheduleDto
{
    [Required]
    public Guid DivisionId { get; set; }

    public DateTime? PublishAt { get; set; }

    [Required]
    public DateTime DueAt { get; set; }
}
