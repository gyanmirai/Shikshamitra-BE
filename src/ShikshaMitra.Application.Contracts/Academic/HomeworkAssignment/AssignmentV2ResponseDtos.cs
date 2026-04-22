using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class AssignmentV2CreateResponseDto
{
    public string Message { get; set; } = string.Empty;
    public AssignmentV2Dto Data { get; set; } = null!;
}

public class AssignmentV2ResponseDto
{
    public AssignmentV2Dto Data { get; set; } = null!;
}

public class AssignmentV2ListResponseDto
{
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<AssignmentV2Dto> Items { get; set; } = new();
}

public class AssignmentStatusUpdateDto
{
    [Required]
    [Range(0, 2)]
    public int Status { get; set; }
}

public class AssignmentStatusUpdateResponseDto
{
    public Guid Id { get; set; }
    public int PreviousStatus { get; set; }
    public int CurrentStatus { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
}

public class GetAssignmentV2ListFilterDto
{
    [Required]
    public Guid StandardId { get; set; }

    [Required]
    public Guid DivisionId { get; set; }

    [Required]
    public Guid SubjectId { get; set; }

    [Required]
    public DateTime AssignmentDate { get; set; }

    public int Page { get; set; } = 1;

    [Range(10, 100)]
    public int PageSize { get; set; } = 20;

    [StringLength(100, MinimumLength = 2)]
    public string? Search { get; set; }
}
