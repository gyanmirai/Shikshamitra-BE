using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp;

namespace ShikshaMitra.HR.Audit;

[RemoteService(false)]
public class HrAuditTrailAccessService(IPermissionChecker permissionChecker, IUserScopeService userScopeService) : ApplicationService, ITransientDependency
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
			if (HrAuditTrailQuery.Normalize(entityType) == "payroll_run")
			{
				await CheckPermissionAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.ViewAudit);
				if (!await HasSchoolWidePayrollScopeAsync())
					throw new AbpAuthorizationException("School-wide HR scope is required for payroll activity.");
				return PayrollAuditEntityTypes;
			}
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
		if (await permissionChecker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.ViewAudit)
			&& await HasSchoolWidePayrollScopeAsync())
		{
			allowed.AddRange(PayrollAuditEntityTypes);
		}
		return allowed.Distinct().ToArray();
	}

	private async Task<bool> HasSchoolWidePayrollScopeAsync()
	{
		if (!CurrentTenant.Id.HasValue || !CurrentUser.Id.HasValue)
			return false;
		var scope = await userScopeService.GetUserScopeAsync(CurrentUser.Id.Value);
		return scope.TenantId == CurrentTenant.Id && HrUserScopeAccess.HasHrWideAccess(scope);
	}

	private static string ResolveAuditPermission(string? entityType)
	{
		return HrAuditTrailQuery.Normalize(entityType ?? string.Empty) switch
		{
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
