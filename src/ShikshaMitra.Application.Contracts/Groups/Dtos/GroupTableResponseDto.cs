using System;
using System.Collections.Generic;

namespace ShikshaMitra.Groups.Dtos;

public class GroupTableResponseDto
{
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public long TotalCount { get; set; }
    public int TotalPages { get; set; }
    public List<GroupTableRowDto> Items { get; set; } = new();
}

public class GroupTableRowDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public int TotalAudience { get; set; }
    public int Students { get; set; }
    public int Employees { get; set; }
    public DateTime CreatedAt { get; set; }
}
