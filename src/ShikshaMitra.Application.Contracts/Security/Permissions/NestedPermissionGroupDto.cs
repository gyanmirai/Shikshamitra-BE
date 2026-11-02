using System.Collections.Generic;

namespace ShikshaMitra.Security.Permissions;

public class NestedPermissionGroupDto
{
	public string Name { get; set; } = null!; // The response mapper populates these members after construction.
	public string DisplayName { get; set; } = null!;
	public string? DisplayNameKey { get; set; }
	public string? DisplayNameResource { get; set; }

	public List<PermissionWithChildrenDto> Permissions { get; set; } = new List<PermissionWithChildrenDto>();
}
