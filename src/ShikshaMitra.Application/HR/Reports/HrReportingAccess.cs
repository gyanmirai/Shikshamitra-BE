using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;

namespace ShikshaMitra.HR.Reports;

public class HrReportingAccess(IPermissionChecker permissions, IIdentityUserRepository users, IRepository<IdentityRole, Guid> roles, ICurrentTenant tenant) : ITransientDependency
{
	public async Task<ClaimsPrincipal> PrincipalAsync(Guid userId)
	{
		if (!tenant.Id.HasValue) throw new AbpAuthorizationException("Reports require a school workspace.");
		var user = await users.FindAsync(userId, includeDetails: true);
		if (user == null || !user.IsActive || user.TenantId != tenant.Id) throw new AbpAuthorizationException();
		var roleIds = user.Roles.Select(x => x.RoleId).ToArray();
		var currentRoles = await roles.GetListAsync(x => Enumerable.Contains(roleIds, x.Id));
		var claims = new List<Claim> { new(AbpClaimTypes.UserId, userId.ToString()), new(AbpClaimTypes.UserName, user.UserName) };
		if (tenant.Id.HasValue) claims.Add(new(AbpClaimTypes.TenantId, tenant.Id.Value.ToString()));
		claims.AddRange(currentRoles.Select(x => new Claim(AbpClaimTypes.Role, x.Name)));
		return new ClaimsPrincipal(new ClaimsIdentity(claims, "Reporting", AbpClaimTypes.UserName, AbpClaimTypes.Role));
	}
	public async Task<bool> CanReadAsync(string dataset, Guid userId)
	{
		var principal = await PrincipalAsync(userId);
		if (!await permissions.IsGrantedAsync(principal, ShikshaMitraSubmodulePermissions.HR.Reports.View)) return false;
		var permission = dataset switch
		{
			"employee" => ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View,
			"attendance" => ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_AttendanceTracking.View,
			"leave" or "leave_balances" => ShikshaMitraSubmodulePermissions.HR.LeaveManagement_LeaveCenter.View,
			"payroll_statement" or "payroll_register" or "payroll" => ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.View,
			"policies" => ShikshaMitraSubmodulePermissions.HR.Policies.View,
			"performance" => ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.View,
			_ => ""
		};
		return !string.IsNullOrEmpty(permission) && (await permissions.IsGrantedAsync(principal, permission)
			|| (dataset == "performance" && await permissions.IsGrantedAsync(principal, ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.View)));
	}
	public async Task EnsureAsync(string dataset, Guid userId)
	{
		if (!await CanReadAsync(dataset, userId)) throw new AbpAuthorizationException("Report dataset access denied.");
	}
	public async Task<bool> CanReadConfidentialAsync(Guid userId)
	{
		var principal = await PrincipalAsync(userId);
		return await permissions.IsGrantedAsync(principal, ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.View)
			|| await permissions.IsGrantedAsync(principal, ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ViewConfidentialOutcome);
	}
	public async Task EnsureDesignAsync(HrReportDesignDto definition, Guid userId)
	{
		await EnsureAsync(definition.DataSource, userId);
		if (definition.DataSource != "performance") return;
		if (UsesConfidentialOutcome(definition) && !await CanReadConfidentialAsync(userId)) throw new AbpAuthorizationException("Confidential review outcome access is required.");
	}
	public static bool UsesConfidentialOutcome(HrReportDesignDto definition)
	{
		var fields = definition.Columns.Select(x => x.Field).Concat(definition.Groups).Concat(definition.Sorts.Select(x => x.Field))
			.Concat(definition.Summaries.Select(x => x.Field)).Concat(definition.Filters.Conditions.Select(x => x.Field))
			.Concat(definition.Filters.Groups.SelectMany(x => x.Conditions).Select(x => x.Field)).Append(definition.Chart?.ValueField ?? "").Append(definition.Chart?.CategoryField ?? "");
		return fields.Any(x => x is "finalScore" or "finalRating");
	}
}
