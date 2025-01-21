using System;

namespace ShikshaMitra.Security.OrganizationUnits.Dtos;

public class GetUnAddRoleInput : PagingBase
{
	public Guid OrganizationUnitId { get; set; }

	public string Filter { get; set; }
}