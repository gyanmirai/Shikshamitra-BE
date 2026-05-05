using System;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class GetAssessmentsInput
{
    public Guid StandardId { get; set; }
    public Guid DivisionId { get; set; }
    public Guid SubjectId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public SubmissionStatus? Status { get; set; }
    public string? Search { get; set; }
}
