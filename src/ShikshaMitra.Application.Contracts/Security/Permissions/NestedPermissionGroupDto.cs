using System.Collections.Generic;

namespace ShikshaMitra.Security.Permissions;

public class NestedPermissionGroupDto
{
	public string Name { get; set; }
	public string DisplayName { get; set; }
	public string DisplayNameKey { get; set; }
	public string DisplayNameResource { get; set; }

	public List<PermissionWithChildrenDto> Permissions { get; set; } = new List<PermissionWithChildrenDto>();
}
