using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Reports;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.TenantSettings.Reports;

public class InstituteReportAccess(HrReportingAccess identities, IPermissionChecker permissions) : ITransientDependency
{
	public async Task EnsureAsync(Guid userId, string? reportType = null, bool generate = false)
	{
		var principal = await identities.PrincipalAsync(userId);
		var required = new[] { InstituteReportPermissions.View }
			.Concat(generate ? [InstituteReportPermissions.Add] : Array.Empty<string>())
			.Concat(reportType == null ? [] : InstituteReportCatalog.SourcePermissions(reportType));
		foreach (var permission in required)
			if (!await permissions.IsGrantedAsync(principal, permission)) throw new AbpAuthorizationException("Institute Setup report access denied.");
	}
	public async Task<bool> CanReadAsync(Guid userId, string reportType)
	{
		try { await EnsureAsync(userId, reportType); return true; }
		catch (AbpAuthorizationException) { return false; }
	}
}
