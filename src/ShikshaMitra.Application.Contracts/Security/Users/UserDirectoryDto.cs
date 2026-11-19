using System;
using System.Collections.Generic;

namespace ShikshaMitra.Security.Users;

public sealed class UserDirectoryDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Surname { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public List<string> RoleNames { get; set; } = [];
}
