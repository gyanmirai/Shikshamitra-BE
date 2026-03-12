using System;

namespace ShikshaMitra.Security.OrganizationUnits.Dtos;

public class RemoveUserToOrganizationUnitInput
{
    public Guid UserId { get; set; }

    public Guid OrganizationUnitId { get; set; }
}