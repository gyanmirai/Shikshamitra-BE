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

		if (!await HasAnyPermissionAsync(StudentAttendancePermissions) ||
			!await permissionChecker.IsGrantedAsync(ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View))
		{
			return;
		}

		try
		{
			var attendance = await studentListAppService.GetAttendanceDashboardSummaryAsync(
				new GetStudentAttendanceDashboardSummaryInput
				{
					AcademicYearId = Guid.TryParse(summary.AcademicYearId, out var year) ? year : null,
					StartDate = DateTime.ParseExact(range.From!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
					EndDate = DateTime.ParseExact(range.To!, "yyyy-MM-dd", CultureInfo.InvariantCulture)
				});
			var percentage = attendance.SchoolAttendancePercentage;
			var status = !percentage.HasValue ? "info" : attendance.IsProvisional ? "warning" : percentage >= 90 ? "success" : percentage >= 75 ? "warning" : "error";
			var classCount = attendance.Classes.Count;
			var divisionCount = attendance.Classes.Sum(x => x.Divisions.Count);
			var subtitle = classCount == 0
				? "No scoped attendance summary found"
				: $"{attendance.RecordedDays}/{attendance.ExpectedDays} expected student-days marked; {classCount} classes, {divisionCount} divisions";
			if (attendance.IsProvisional) subtitle += "; provisional";
			var value = percentage.HasValue ? $"{percentage:0.##}%" : "—";

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
