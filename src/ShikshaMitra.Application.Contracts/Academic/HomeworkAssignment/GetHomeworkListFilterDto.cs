using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class GetHomeworkListFilterDto
{
    [Required]
    public Guid StandardId { get; set; }

    [Required]
    public Guid DivisionId { get; set; }

    [Required]
    public Guid SubjectId { get; set; }

    [Required]
    public DateTime HomeworkDate { get; set; }

    public int Page { get; set; } = 1;

    [Range(10, 100)]
    public int PageSize { get; set; } = 20;

    [StringLength(100, MinimumLength = 2)]
    public string? Search { get; set; }
}
