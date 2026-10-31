using System;
using System.Collections.Generic;

namespace ShikshaMitra.Security.OrganizationUnits.Dtos;

public class AddRoleToOrganizationUnitInput
{
    public List<Guid> RoleId { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.

    public Guid OrganizationUnitId { get; set; }
}