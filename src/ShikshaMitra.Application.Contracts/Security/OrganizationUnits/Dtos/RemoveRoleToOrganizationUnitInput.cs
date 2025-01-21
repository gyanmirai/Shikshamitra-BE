using System;

namespace ShikshaMitra.Security.OrganizationUnits.Dtos;

public class RemoveRoleToOrganizationUnitInput
{
    public Guid RoleId { get; set; }
    
    public Guid OrganizationUnitId { get; set; }
}