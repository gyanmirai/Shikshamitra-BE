using System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace ShikshaMitra.Academic.Calendar;

/// <summary>
/// Writes a rich AcademicCalendarAuditLog row: resolves the actor's name/role/email,
/// computes a structured field-level diff from before/after JSON snapshots, and captures
/// request metadata (source/device/browser/IP). Adapted from HrAuditWriter's Diff/actor-resolution
/// pattern for the Calendar module.
///
/// Callers pass already-serialized JSON snapshots (captured immediately before/after mutating the
/// tracked entity) rather than live object references, since entities here use private setters and
/// are mutated in place — serializing a live reference "before" would reflect its already-mutated
/// state by the time this method runs.
/// </summary>
public class AcademicCalendarAuditWriter(
    IRepository<AcademicCalendarAuditLog, Guid> repository,
    IIdentityUserRepository userRepository,
    IRepository<IdentityRole, Guid> roleRepository,
    IHttpContextAccessor httpContextAccessor,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    IGuidGenerator guidGenerator) : ITransientDependency
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] DefaultIgnoredDiffFields =
    [
        "id",
        "tenantId",
        "creatorId",
        "creationTime",
        "lastModifierId",
        "lastModificationTime",
        "deleterId",
        "deletionTime",
        "isDeleted"
    ];

    public async Task WriteAsync(
        Guid academicCalendarId,
        CalendarAuditAction action,
        string? beforeJson,
        string? afterJson,
        string? entityType = null,
        string? entityName = null,
        string? reason = null,
        Guid? academicCalendarDayId = null,
        string module = "CalendarManagement",
        string? subModule = null,
        string? correlationId = null)
    {
        var (actorName, actorRole, actorEmail) = await ResolveActorAsync();
        var changedFields = Diff(beforeJson, afterJson);
        var changedFieldsJson = changedFields.Count > 0 ? JsonSerializer.Serialize(changedFields, JsonOptions) : null;

        var (source, ipAddress, device, browser, operatingSystem, requestPath, httpMethod) = ResolveRequestContext();

        var log = new AcademicCalendarAuditLog(
            guidGenerator.Create(),
            currentTenant.Id,
            academicCalendarId,
            action,
            beforeJson ?? string.Empty,
            afterJson ?? string.Empty,
            currentUser.Id ?? Guid.Empty,
            reason,
            academicCalendarDayId,
            module: module,
            subModule: subModule,
            entityType: entityType,
            entityName: entityName,
            changedFieldsJson: changedFieldsJson,
            actorNameSnapshot: actorName,
            actorRoleSnapshot: actorRole,
            actorEmailSnapshot: actorEmail,
            source: source,
            ipAddress: ipAddress,
            device: device,
            browser: browser,
            operatingSystem: operatingSystem,
            requestPath: requestPath,
            httpMethod: httpMethod,
            correlationId: correlationId
        );

        await repository.InsertAsync(log);
    }

    /// <summary>Diffs two JSON object snapshots. Returns no fields (rather than "everything changed")
    /// when either side is missing — that's a create/delete, not a field-level update.</summary>
    public static List<AuditLogDiffDto> Diff(string? beforeJson, string? afterJson, params string[] ignoredFields)
    {
        if (string.IsNullOrWhiteSpace(beforeJson) || string.IsNullOrWhiteSpace(afterJson))
        {
            return [];
        }

        var ignored = DefaultIgnoredDiffFields.Concat(ignoredFields).Select(x => x.ToLowerInvariant()).ToHashSet();

        Dictionary<string, string?> beforeValues;
        Dictionary<string, string?> afterValues;
        try
        {
            using var beforeDoc = JsonDocument.Parse(beforeJson);
            using var afterDoc = JsonDocument.Parse(afterJson);
            beforeValues = beforeDoc.RootElement.EnumerateObject()
                .ToDictionary(x => x.Name, x => x.Value.ValueKind == JsonValueKind.Null ? null : x.Value.GetRawText());
            afterValues = afterDoc.RootElement.EnumerateObject()
                .ToDictionary(x => x.Name, x => x.Value.ValueKind == JsonValueKind.Null ? null : x.Value.GetRawText());
        }
        catch (JsonException)
        {
            return [];
        }

        return beforeValues.Keys.Union(afterValues.Keys)
            .Where(x => !ignored.Contains(x.ToLowerInvariant()))
            .Where(x => !AreEquivalentValues(x, beforeValues.GetValueOrDefault(x), afterValues.GetValueOrDefault(x)))
            .Select(x =>
            {
                var hasOld = beforeValues.ContainsKey(x);
                var hasNew = afterValues.ContainsKey(x);
                var changeType = !hasOld && hasNew ? "added" : hasOld && !hasNew ? "removed" : "updated";
                return new AuditLogDiffDto
                {
                    FieldName = x,
                    DisplayName = ToDisplayName(x),
                    OldValue = Trim(beforeValues.GetValueOrDefault(x)),
                    NewValue = Trim(afterValues.GetValueOrDefault(x)),
                    ChangeType = changeType
                };
            })
            .OrderBy(x => x.FieldName)
            .ToList();
    }

    private async Task<(string? Name, string? Role, string? Email)> ResolveActorAsync()
    {
        if (!currentUser.Id.HasValue)
        {
            return (currentUser.UserName, null, currentUser.Email);
        }

        var user = await userRepository.FindAsync(currentUser.Id.Value, includeDetails: true);
        if (user == null)
        {
            return (currentUser.UserName, null, currentUser.Email);
        }

        var name = string.Join(" ", new[] { user.Name, user.Surname }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var displayName = string.IsNullOrWhiteSpace(name) ? user.UserName : name;

        string? roleName = null;
        if (user.Roles.Count > 0)
        {
            var roleIds = user.Roles.Select(x => x.RoleId).Distinct().ToList();
            var roles = await roleRepository.GetListAsync(r => roleIds.Contains(r.Id));
            roleName = roles.FirstOrDefault()?.Name;
        }

        return (displayName, roleName, user.Email);
    }

    private (CalendarAuditSource Source, string? IpAddress, string? Device, string? Browser, string? OperatingSystem, string? RequestPath, string? HttpMethod) ResolveRequestContext()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return (CalendarAuditSource.SystemJob, null, null, null, null, null, null);
        }

        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var requestPath = httpContext.Request.Path.ToString();
        var httpMethod = httpContext.Request.Method;

        var (device, browser, os) = ParseUserAgent(userAgent);
        var source = device == "Mobile" ? CalendarAuditSource.Mobile : CalendarAuditSource.Web;

        return (source, ipAddress, device, browser, os, requestPath, httpMethod);
    }

    private static (string Device, string? Browser, string? OperatingSystem) ParseUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return ("Unknown", null, null);
        }

        var ua = userAgent.ToLowerInvariant();
        var device = ua.Contains("mobile") || (ua.Contains("android") && !ua.Contains("tablet"))
            ? "Mobile"
            : ua.Contains("tablet") || ua.Contains("ipad")
                ? "Tablet"
                : "Desktop";

        string? browser =
            ua.Contains("edg/") ? "Edge" :
            ua.Contains("chrome/") && !ua.Contains("edg/") ? "Chrome" :
            ua.Contains("firefox/") ? "Firefox" :
            ua.Contains("safari/") && !ua.Contains("chrome/") ? "Safari" :
            null;

        string? os =
            ua.Contains("windows") ? "Windows" :
            ua.Contains("mac os") ? "macOS" :
            ua.Contains("android") ? "Android" :
            ua.Contains("iphone") || ua.Contains("ipad") ? "iOS" :
            ua.Contains("linux") ? "Linux" :
            null;

        return (device, browser, os);
    }

    private static string ToDisplayName(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName))
        {
            return fieldName;
        }
        var withSpaces = System.Text.RegularExpressions.Regex.Replace(fieldName, "(?<!^)([A-Z])", " $1");
        return char.ToUpperInvariant(withSpaces[0]) + withSpaces[1..];
    }

    private static string? Trim(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }
        var normalized = value.Trim('"');
        return normalized.Length <= 500 ? normalized : normalized[..500] + "...";
    }

    // Calendar dates are date-only business values. JSON serialization may add/remove an offset
    // (for example 2026-08-27T00:00:00+05:30 vs 2026-08-27T00:00:00) without changing the date.
    private static bool AreEquivalentValues(string fieldName, string? before, string? after)
    {
        if (string.Equals(before, after, StringComparison.Ordinal))
        {
            return true;
        }

        if (!fieldName.EndsWith("Date", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return TryGetCalendarDate(before, out var beforeDate)
            && TryGetCalendarDate(after, out var afterDate)
            && beforeDate == afterDate;
    }

    private static bool TryGetCalendarDate(string? value, out DateOnly date)
    {
        date = default;
        var normalized = value?.Trim('"');
        return normalized?.Length >= 10
            && DateOnly.TryParseExact(normalized[..10], "yyyy-MM-dd", out date);
    }
}
