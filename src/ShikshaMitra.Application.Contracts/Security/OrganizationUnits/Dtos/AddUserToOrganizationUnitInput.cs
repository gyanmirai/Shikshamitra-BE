using System;
using System.Collections.Generic;

namespace ShikshaMitra.Security.OrganizationUnits.Dtos;

public class AddUserToOrganizationUnitInput
{
    public List<Guid> UserId { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.

    public Guid OrganizationUnitId { get; set; }
}