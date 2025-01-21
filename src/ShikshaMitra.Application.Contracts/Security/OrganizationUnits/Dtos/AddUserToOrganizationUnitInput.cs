using System;
using System.Collections.Generic;

namespace ShikshaMitra.Security.OrganizationUnits.Dtos;

public class AddUserToOrganizationUnitInput
{
    public List<Guid> UserId { get; set; }
    
    public Guid OrganizationUnitId { get; set; }
}