using System.Collections.Generic;

namespace ShikshaMitra.Security.UserScope;

/// <summary>
/// Configuration options for UserScope system.
/// Configured via appsettings.json under "UserScope" section.
/// </summary>
public class UserScopeOptions
{
    /// <summary>
    /// Legacy configuration retained for compatibility, not an authorization cache switch.
    /// Effective scope is always resolved from current data with unit-of-work-local reuse only.
    /// </summary>
    public bool CacheEnabled { get; set; } = true;

    /// <summary>
    /// Legacy configuration retained for compatibility and ignored by authorization resolution.
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
