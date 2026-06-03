using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp;

namespace ShikshaMitra.HR.Audit;

[RemoteService(false)]
public class HrAuditTrailAccessService(IPermissionChecker permissionChecker) : ApplicationService, ITransientDependency
{
	private static readonly string[] PolicyAuditEntityTypes =
	[
		"work_calendar_policy",
		"payroll_policy",
		"attendance_policy",
		"policy_assignment",
		"policy_version"
	];
	private static readonly string[] AttendanceAuditEntityTypes = ["employee_attendance"];
	private static readonly string[] PayrollAuditEntityTypes = ["payroll_run"];

	public async Task<string[]?> ResolveAllowedEntityTypesAsync(string? entityType)
	{
		if (!string.IsNullOrWhiteSpace(entityType))
		{
			await CheckPermissionAsync(ResolveAuditPermission(entityType));
			return null;
		}

		var allowed = new List<string>();
		if (await permissionChecker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.Policies.ViewAudit))
		{
			allowed.AddRange(PolicyAuditEntityTypes);
		}
		if (await permissionChecker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_Evaluations.ViewAudit))
		{
			allowed.AddRange(AttendanceAuditEntityTypes);
		}
		if (await permissionChecker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.ViewAudit))
		{
			allowed.AddRange(PayrollAuditEntityTypes);
		}
		return allowed.Distinct().ToArray();
	}

	private static string ResolveAuditPermission(string? entityType)
	{
		return HrAuditTrailQuery.Normalize(entityType ?? string.Empty) switch
		{
			"payroll_run" => ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.ViewAudit,
			"employee_attendance" => ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_Evaluations.ViewAudit,
			_ => ShikshaMitraSubmodulePermissions.HR.Policies.ViewAudit
		};
	}

	private async Task CheckPermissionAsync(string permissionName)
	{
		if (!await permissionChecker.IsGrantedAsync(permissionName))
		{
			throw new AbpAuthorizationException($"Required permission: {permissionName}");
		}
	}
}
