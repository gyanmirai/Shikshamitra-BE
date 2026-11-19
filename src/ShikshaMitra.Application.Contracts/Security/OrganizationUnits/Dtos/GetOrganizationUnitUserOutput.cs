using System;

namespace ShikshaMitra.Security.OrganizationUnits.Dtos;

public class GetOrganizationUnitUserOutput
{
    public Guid Id { get; set; }

    public string UserName { get; set; } = null!; // The response mapper populates these members after construction.

    public string? Email { get; set; }
}