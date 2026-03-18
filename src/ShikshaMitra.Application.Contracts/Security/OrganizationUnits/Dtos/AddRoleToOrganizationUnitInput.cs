using System;
using System.Collections.Generic;

namespace ShikshaMitra.Security.OrganizationUnits.Dtos;

public class AddRoleToOrganizationUnitInput
{
    public List<Guid> RoleId { get; set; }

    public Guid OrganizationUnitId { get; set; }
}