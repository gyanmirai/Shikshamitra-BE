using System.Collections.Generic;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectAssignmentPagedResponse
{
    public long TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<SubjectAssignmentGroupDto> Items { get; set; } = new();
}
