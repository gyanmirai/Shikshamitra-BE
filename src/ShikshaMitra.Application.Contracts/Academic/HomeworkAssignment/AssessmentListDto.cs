using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class AssessmentListDto<T>
{
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<T> Items { get; set; } = new();
}
