using System.Collections.Generic;


namespace ShikshaMitra.Security.Permissions;

public class GetNestedPermissionListResultDto
{
	public string EntityDisplayName { get; set; } = null!; // The response mapper populates these members after construction.
	public List<NestedPermissionGroupDto> Groups { get; set; } = new List<NestedPermissionGroupDto>();
}
