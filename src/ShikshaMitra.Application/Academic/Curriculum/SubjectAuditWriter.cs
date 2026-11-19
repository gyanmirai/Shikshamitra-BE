using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Curriculum.Settings;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectAuditWriter(
    IRepository<SubjectAuditLog, Guid> repository,
    IIdentityUserRepository userRepository,
    IRepository<IdentityRole, Guid> roleRepository,
    IHttpContextAccessor httpContextAccessor,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    IGuidGenerator guidGenerator) : ITransientDependency
{
    public sealed record ActorSnapshot(string? Name, string? Role, string? Email);

    public async Task<ActorSnapshot> CaptureActorAsync()
    {
        var (name, role, email) = await ResolveActorAsync();
        return new ActorSnapshot(name, role, email);
    }

    public async Task WriteCreatedManyAsync(IReadOnlyList<(Guid SubjectId, string Snapshot, string Name)> subjects)
    {
        if (subjects.Count == 0) return;
        var actor = await CaptureActorAsync();
        var (source, ipAddress, device, browser, operatingSystem, requestPath, httpMethod) = ResolveRequestContext();
        var logs = subjects.Select(subject => new SubjectAuditLog(
            guidGenerator.Create(), currentTenant.Id, subject.SubjectId, "Created", string.Empty, subject.Snapshot,
            currentUser.Id ?? Guid.Empty, module: "SubjectCreation", entityType: "subject", entityName: subject.Name,
            actorNameSnapshot: actor.Name, actorRoleSnapshot: actor.Role, actorEmailSnapshot: actor.Email,
            source: source, ipAddress: ipAddress, device: device, browser: browser,
            operatingSystem: operatingSystem, requestPath: requestPath, httpMethod: httpMethod)).ToList();
        await repository.InsertManyAsync(logs);
    }

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
        Guid subjectId,
        string action,
        string? beforeJson,
        string? afterJson,
        string? entityType = null,
        string? entityName = null,
        string? reason = null,
        string module = "SubjectCreation",
        string? subModule = null,
        string? correlationId = null,
        ActorSnapshot? actor = null)
    {
        var (actorName, actorRole, actorEmail) = actor ?? await CaptureActorAsync();
        var changedFields = Diff(beforeJson, afterJson);
        var changedFieldsJson = changedFields.Count > 0 ? JsonSerializer.Serialize(changedFields, JsonOptions) : null;

        var (source, ipAddress, device, browser, operatingSystem, requestPath, httpMethod) = ResolveRequestContext();

        var log = new SubjectAuditLog(
            guidGenerator.Create(),
            currentTenant.Id,
            subjectId,
            action,
            beforeJson ?? string.Empty,
            afterJson ?? string.Empty,
            currentUser.Id ?? Guid.Empty,
            reason,
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
            .Where(x => !string.Equals(beforeValues.GetValueOrDefault(x), afterValues.GetValueOrDefault(x), StringComparison.Ordinal))
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

    private (string? Source, string? IpAddress, string? Device, string? Browser, string? OperatingSystem, string? RequestPath, string? HttpMethod) ResolveRequestContext()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return ("SystemJob", null, null, null, null, null, null);
        }

        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var requestPath = httpContext.Request.Path.ToString();
        var httpMethod = httpContext.Request.Method;

        var (device, browser, os) = ParseUserAgent(userAgent);
        var source = device == "Mobile" ? "Mobile" : "Web";

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
}
