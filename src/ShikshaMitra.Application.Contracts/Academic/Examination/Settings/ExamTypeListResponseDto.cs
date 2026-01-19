using System.Collections.Generic;

namespace ShikshaMitra.Academic.Examination.Settings;

public class ExamTypeListResponseDto
{
    public List<ExamTypeDto> Items { get; set; } = new();
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public long TotalCount { get; set; }
}
