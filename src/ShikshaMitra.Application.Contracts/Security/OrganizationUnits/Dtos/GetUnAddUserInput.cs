using System;

namespace ShikshaMitra.Security.OrganizationUnits.Dtos;

public class GetUnAddUserInput : PagingBase
{
    public Guid OrganizationUnitId { get; set; }

    public string Filter { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.
}