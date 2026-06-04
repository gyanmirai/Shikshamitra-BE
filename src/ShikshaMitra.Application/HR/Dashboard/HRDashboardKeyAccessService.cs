using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Validation;

namespace ShikshaMitra.HR.Dashboard;

public class HRDashboardKeyAccessService(IAuthorizationService authorizationService) : ITransientDependency
{
	public async Task<string> NormalizeAndAuthorizeAsync(string dashboardKey)
	{
		var normalized = NormalizeDashboardKey(dashboardKey);
		await authorizationService.CheckAsync(ResolvePermission(normalized));
		return normalized;
	}

	private static string NormalizeDashboardKey(string dashboardKey)
	{
		var normalized = HrDashboardKeys.All.FirstOrDefault(key =>
			string.Equals(key, dashboardKey, StringComparison.OrdinalIgnoreCase));
		if (normalized == null)
		{
			throw new AbpValidationException(
				"Dashboard key is invalid.",
				[new ValidationResult("Dashboard key must be Main, Attendance, or Payroll.", ["dashboardKey"])]);
		}

		return normalized;
	}

	private static string ResolvePermission(string dashboardKey) => dashboardKey switch
	{
		HrDashboardKeys.Main => ShikshaMitraSubmodulePermissions.HR.Dashboard.View,
		HrDashboardKeys.Attendance => ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_Dashboard.View,
		HrDashboardKeys.Payroll => ShikshaMitraSubmodulePermissions.HR.PayrollManagement_Dashboard.View,
		_ => ShikshaMitraSubmodulePermissions.HR.Dashboard.View
	};
}
