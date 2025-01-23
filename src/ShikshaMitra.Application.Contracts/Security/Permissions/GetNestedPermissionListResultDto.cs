using System.Collections.Generic;


namespace ShikshaMitra.Security.Permissions;

public class GetNestedPermissionListResultDto
{
	public string EntityDisplayName { get; set; }
	public List<NestedPermissionGroupDto> Groups { get; set; } = new List<NestedPermissionGroupDto>();
}
