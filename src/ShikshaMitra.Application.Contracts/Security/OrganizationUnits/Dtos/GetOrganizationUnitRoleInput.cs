using System;

namespace ShikshaMitra.Security.OrganizationUnits.Dtos;

public class GetOrganizationUnitRoleInput : PagingBase
{
	public Guid OrganizationUnitId { get; set; }
}