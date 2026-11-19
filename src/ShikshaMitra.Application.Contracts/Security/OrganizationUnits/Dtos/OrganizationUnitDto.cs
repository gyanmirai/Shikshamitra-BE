using System;

namespace ShikshaMitra.Security.OrganizationUnits.Dtos;

public class OrganizationUnitDto
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public string Code { get; set; } = null!; // The response mapper populates these members after construction.

    public string DisplayName { get; set; } = null!;

    public  Guid? ParentId { get;  set; }
}