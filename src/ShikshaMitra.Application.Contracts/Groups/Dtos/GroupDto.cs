using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Groups.Dtos;

public class GroupDto : EntityDto<Guid>
{
    public string Name { get; set; }
    public string? Description { get; set; }
    public GroupAudienceType AudienceType { get; set; }
    public int TotalAudience { get; set; }
    public int Students { get; set; }
    public int Employees { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class GroupDetailsDto : GroupDto
{
    public DateTime UpdatedAt { get; set; }
}
