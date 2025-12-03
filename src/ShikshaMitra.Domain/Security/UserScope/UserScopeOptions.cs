using System.Collections.Generic;

namespace ShikshaMitra.Security.UserScope;

/// <summary>
/// Configuration options for UserScope system.
/// Configured via appsettings.json under "UserScope" section.
/// </summary>
public class UserScopeOptions
{
    /// <summary>
    /// Enables or disables caching of user scope contexts.
    /// Default: true
    /// </summary>
    public bool CacheEnabled { get; set; } = true;

    /// <summary>
    /// Duration in minutes for which user scope is cached.
    /// Default: 15 minutes
    /// </summary>
    public int CacheDurationMinutes { get; set; } = 15;

    /// <summary>
    /// Enables or disables the global EF Core query filter.
    /// Default: true
    /// WARNING: Disabling this will remove automatic scope filtering!
    /// </summary>
    public bool EnableGlobalFilter { get; set; } = true;

    /// <summary>
    /// List of role names that have admin privileges (bypass all scope checks).
    /// Default: ["admin", "super-admin"]
    /// </summary>
    public List<string> AdminRoles { get; set; } = new() { "admin", "super-admin" };
}
