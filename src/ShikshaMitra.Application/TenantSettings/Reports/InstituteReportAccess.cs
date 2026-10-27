using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.HR.Reports;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.Reports;

public class InstituteReportAccess(HrReportingAccess identities, IPermissionChecker permissions,
	IUnitOfWorkManager units, ICurrentTenant tenant) : ITransientDependency
{
	private sealed record AccessContext(ClaimsPrincipal Principal, Dictionary<string, bool> Grants);
	private async Task<AccessContext> ContextAsync(Guid userId, bool refresh = false)
	{
		var key = $"InstituteReportAccess:{tenant.Id}:{userId}";
		var items = units.Current?.Items;
		if (!refresh && items?.TryGetValue(key, out var cached) == true && cached is AccessContext context) return context;
		var result = new AccessContext(await identities.PrincipalAsync(userId), new(StringComparer.Ordinal));
		if (items != null) items[key] = result;
		return result;
	}
	public async Task<ClaimsPrincipal> PrincipalAsync(Guid userId, bool refresh = false) => (await ContextAsync(userId, refresh)).Principal;
	public async Task EnsureAsync(Guid userId, string? reportType = null, bool generate = false, bool refresh = false)
	{
		var context = await ContextAsync(userId, refresh);
		var required = new[] { InstituteReportPermissions.View }
			.Concat(generate ? [InstituteReportPermissions.Add] : Array.Empty<string>())
			.Concat(reportType == null ? [] : InstituteReportCatalog.SourcePermissions(reportType));
		foreach (var permission in required)
		{
			if (!context.Grants.TryGetValue(permission, out var granted))
				context.Grants[permission] = granted = await permissions.IsGrantedAsync(context.Principal, permission);
			if (!granted) throw new AbpAuthorizationException("Institute Setup report access denied.");
		}
	}
	public async Task<bool> CanReadAsync(Guid userId, string reportType)
	{
		try { await EnsureAsync(userId, reportType); return true; }
		catch (AbpAuthorizationException) { return false; }
	}
	public async Task<string[]> ReadableTypesAsync(Guid userId)
	{
		await EnsureAsync(userId);
		var result = new List<string>();
		foreach (var definition in InstituteReportCatalog.All)
			if (await CanReadAsync(userId, definition.Id)) result.Add(definition.Id);
		return result.ToArray();
	}
}
