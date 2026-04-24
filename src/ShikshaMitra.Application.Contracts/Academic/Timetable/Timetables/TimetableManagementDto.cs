using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Timetable.Timetables;

public class TimetableManagementResponseDto
{
    public Guid StandardId { get; set; }
    public string StandardName { get; set; } = string.Empty;
    public List<DivisionTimetableStatusDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
}

public class DivisionTimetableStatusDto
{
    public Guid DivisionId { get; set; }
    public string DivisionName { get; set; } = string.Empty;
    public TimetableStatus Status { get; set; }
}

public class GetTimetableManagementInput : PagedAndSortedResultRequestDto
{
    public string? Search { get; set; }
}
