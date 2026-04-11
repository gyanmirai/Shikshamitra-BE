using System;

namespace ShikshaMitra.Security.OrganizationUnits.Dtos;

public class GetUnAddUserOutput
{
    public Guid Id { get; set; }

    public string UserName { get; set; }

    public string Email { get; set; }
}