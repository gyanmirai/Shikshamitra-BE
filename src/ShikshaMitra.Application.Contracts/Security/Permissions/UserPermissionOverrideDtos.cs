using System.Collections.Generic;
using Volo.Abp.PermissionManagement;

namespace ShikshaMitra.Security.Permissions;

public class UserPermissionOverridesDto : GetPermissionListResultDto
{
    public Dictionary<string, string> Overrides { get; set; } = new();
}

public class UpdateUserPermissionOverridesDto
{
    public UserPermissionOverrideDto[] Permissions { get; set; } = [];
}

public class UserPermissionOverrideDto
{
    public string Name { get; set; } = null!;
    public string State { get; set; } = null!;
}
