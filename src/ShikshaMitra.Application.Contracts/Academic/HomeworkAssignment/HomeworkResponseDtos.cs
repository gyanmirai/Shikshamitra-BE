using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class HomeworkResponseDto
{
    public HomeworkDto Data { get; set; } = null!;
}

public class HomeworkCreateResponseDto
{
    public string Message { get; set; } = string.Empty;
    public HomeworkDto Data { get; set; } = null!;
}

public class HomeworkListResponseDto
{
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<HomeworkDto> Items { get; set; } = new();
}

public class HomeworkStatusUpdateResponseDto
{
    public Guid Id { get; set; }
    public int PreviousStatus { get; set; }
    public int CurrentStatus { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
}
