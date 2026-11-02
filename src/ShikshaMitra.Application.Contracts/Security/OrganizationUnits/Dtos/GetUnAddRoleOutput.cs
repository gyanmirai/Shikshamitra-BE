using System;

namespace ShikshaMitra.Security.OrganizationUnits.Dtos;

public class GetUnAddRoleOutput
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!; // The response mapper populates these members after construction.
}