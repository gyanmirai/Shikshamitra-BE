using System;

namespace ShikshaMitra.Security.OrganizationUnits.Dtos;

public class GetOrganizationUnitUserInput : PagingBase
{
    public Guid OrganizationUnitId { get; set; }

    public string? Filter { get; set; }
}