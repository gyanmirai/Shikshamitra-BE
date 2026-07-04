using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using static ShikshaMitra.Dashboard.DashboardSummaryMutator;
using static ShikshaMitra.Dashboard.DashboardTemplates;
using static ShikshaMitra.Dashboard.DashboardWidgetCatalog;
using static ShikshaMitra.Dashboard.DashboardZones;

namespace ShikshaMitra.Dashboard;

public class DashboardStudentRecordsSnapshotSliceProvider(
	IRepository<Student, Guid> studentRepository,
	StudentScopeAccessService studentScopeAccessService,
	IAsyncQueryableExecuter asyncExecuter,
	IPermissionChecker permissionChecker)
	: IDashboardSummarySliceProvider, ITransientDependency
{
	private const string TotalStudentsWidgetId = "leadership-total-students";

	public async Task ApplyAsync(DashboardSummarySliceContext context)
	{
		var summary = context.Summary;

		if (summary.DashboardTemplate != Leadership)
		{
			return;
		}

		if (!await HasAnyPermissionAsync(WidgetDefinitions[TotalStudentsWidgetId].RequiredPermissions))
		{
			return;
		}

		try
		{
			var students = await studentRepository.GetQueryableAsync();
			var scopedStudents = await studentScopeAccessService.ApplyCurrentUserScopeAsync(students);
			var enrolledStudents = await asyncExecuter.CountAsync(scopedStudents
				.Where(student => student.Status == StudentLifeCycleStatusEnum.Enrolled));
			var subtitle = enrolledStudents == 0
				? "No enrolled students in scope"
				: $"{enrolledStudents} {(enrolledStudents == 1 ? "enrolled student" : "enrolled students")} in scope";

			UpdateWidget(
				summary.KpiCards,
				TotalStudentsWidgetId,
				enrolledStudents,
				enrolledStudents.ToString(CultureInfo.InvariantCulture),
				subtitle,
				"success");
		}
		catch
		{
			SetZoneError(summary, Kpis, "Student records snapshot could not be loaded.");
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
