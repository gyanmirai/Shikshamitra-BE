using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using static ShikshaMitra.Dashboard.DashboardSummaryMutator;
using static ShikshaMitra.Dashboard.DashboardTemplates;
using static ShikshaMitra.Dashboard.DashboardWidgetCatalog;
using static ShikshaMitra.Dashboard.DashboardZones;

namespace ShikshaMitra.Dashboard;

public class DashboardStudentAttendanceSliceProvider(
	IStudentListAppService studentListAppService,
	IPermissionChecker permissionChecker)
	: IDashboardSummarySliceProvider, ITransientDependency
{
	public async Task ApplyAsync(DashboardSummarySliceContext context)
	{
		var summary = context.Summary;
		var range = context.DateRange;

		if (summary.DashboardTemplate is not (Leadership or PrincipalAdmin))
		{
			return;
		}

		if (!await HasAnyPermissionAsync(StudentAttendancePermissions))
		{
			return;
		}

		try
		{
			var attendance = await studentListAppService.GetAttendanceDashboardSummaryAsync(
				new GetStudentAttendanceDashboardSummaryInput
				{
					StartDate = DateTime.ParseExact(range.From!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
					EndDate = DateTime.ParseExact(range.To!, "yyyy-MM-dd", CultureInfo.InvariantCulture)
				});
			var percentage = attendance.SchoolAttendancePercentage;
			var status = percentage >= 90 ? "success" : percentage >= 75 ? "warning" : "error";
			var classCount = attendance.Classes.Count;
			var divisionCount = attendance.Classes.Sum(x => x.Divisions.Count);
			var subtitle = classCount == 0
				? "No scoped attendance summary found"
				: $"{classCount} classes, {divisionCount} divisions in scope";
			var value = $"{percentage:0.##}%";

			if (summary.DashboardTemplate == Leadership)
			{
				Add(summary.KpiCards, Widget("leadership-attendance", "Student Attendance", value, subtitle, status));
				return;
			}

			Add(summary.KpiCards, Widget("principal-student-attendance", "Student Attendance", value, subtitle, status));
		}
		catch
		{
			SetZoneError(summary, Kpis, "Student attendance snapshot could not be loaded.");
		}
	}

	private async Task<bool> HasAnyPermissionAsync(string[] permissionNames)
	{
		foreach (var permissionName in permissionNames)
		{
			if (await permissionChecker.IsGrantedAsync(permissionName))
			{
				return true;
			}
		}

		return false;
	}
}
