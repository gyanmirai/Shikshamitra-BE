using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Finance.Accounts.Reconciliation;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Dashboard;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Records;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using static ShikshaMitra.Dashboard.DashboardSummaryMutator;
using static ShikshaMitra.Dashboard.DashboardTemplates;
using static ShikshaMitra.Dashboard.DashboardWidgetCatalog;
using static ShikshaMitra.Dashboard.DashboardZones;

namespace ShikshaMitra.Dashboard;

public class DashboardActionQueueSliceProvider(
	IRepository<Student, Guid> studentRepository,
	StudentScopeAccessService studentScopeAccessService,
	IEmployeeLeaveRepository employeeLeaveRepository,
	HRDashboardEmployeeScopeReader hrEmployeeScopeReader,
	HRDashboardPayrollReader hrDashboardPayrollReader,
	IUserScopeAccessService userScopeAccessService,
	IReconciliationAppService reconciliationAppService,
	IAsyncQueryableExecuter asyncExecuter,
	IPermissionChecker permissionChecker)
	: IDashboardSummarySliceProvider, ITransientDependency
{
	public async Task ApplyAsync(DashboardSummarySliceContext context)
	{
		await AddAdmissionActionsAsync(context.Summary);
		await AddHrActionsAsync(context.Summary, context.DateRange);
		await AddFinanceActionsAsync(context.Summary, context.DateRange);
	}

	private async Task AddAdmissionActionsAsync(DashboardSummaryDto summary)
	{
		if (summary.DashboardTemplate is not (Leadership or PrincipalAdmin))
		{
			return;
		}

		var permission = WidgetDefinitions["leadership-admission-approvals"].RequiredPermissions;
		if (!await HasAnyPermissionAsync(permission))
		{
			return;
		}

		try
		{
			var students = await studentRepository.GetQueryableAsync();
			var scopedStudents = await studentScopeAccessService.ApplyCurrentUserScopeAsync(students);
			var pendingAdmissions = await asyncExecuter.CountAsync(scopedStudents
				.Where(s => s.Status == StudentLifeCycleStatusEnum.Admission));
			var subtitle = BuildCountSubtitle(pendingAdmissions, "admission waiting for action", "admissions waiting for action");

			UpdateWidget(summary.ActionItems, "leadership-admission-approvals", pendingAdmissions, null, subtitle, CountStatus(pendingAdmissions));
			UpdateWidget(summary.KpiCards, "principal-pending-admissions", null, pendingAdmissions.ToString(CultureInfo.InvariantCulture), subtitle, CountStatus(pendingAdmissions));
		}
		catch
		{
			SetZoneError(
				summary,
				summary.DashboardTemplate == PrincipalAdmin ? Kpis : Actions,
				"Admission approval count could not be loaded.");
		}
	}

	private async Task AddHrActionsAsync(DashboardSummaryDto summary, DashboardDateRangeDto range)
	{
		if (summary.DashboardTemplate is not (Leadership or PrincipalAdmin or FinanceHr))
		{
			return;
		}

		var needsLeave = summary.DashboardTemplate is PrincipalAdmin or FinanceHr &&
			await HasAnyPermissionAsync(WidgetDefinitions["hr-leave-approvals"].RequiredPermissions);
		var needsPayroll = summary.DashboardTemplate == Leadership &&
			await HasAnyPermissionAsync(WidgetDefinitions["leadership-payroll-review"].RequiredPermissions);

		if (!needsLeave && !needsPayroll)
		{
			return;
		}

		try
		{
			var toDate = ParseNormalizedDate(range.To);
			var scope = await userScopeAccessService.GetCurrentUserScopeAsync();

			if (needsLeave)
			{
				var pendingLeave = await CountPendingLeaveApprovalsAsync(scope);
				var subtitle = BuildCountSubtitle(pendingLeave, "leave request awaiting action", "leave requests awaiting action");
				UpdateWidget(summary.ActionItems, "principal-leave-approvals", pendingLeave, null, subtitle, CountStatus(pendingLeave));
				UpdateWidget(summary.ActionItems, "hr-leave-approvals", pendingLeave, null, subtitle, CountStatus(pendingLeave));
			}

			if (needsPayroll)
			{
				var payroll = await hrDashboardPayrollReader.BuildPayrollAsync(toDate.ToString("yyyy-MM", CultureInfo.InvariantCulture));
				var pendingPayroll = payroll.PendingCount + payroll.BlockingIssues.Count;
				var subtitle = pendingPayroll == 0
					? "Payroll readiness has no pending review items"
					: BuildCountSubtitle(pendingPayroll, "payroll review item needs action", "payroll review items need action");
				UpdateWidget(summary.ActionItems, "leadership-payroll-review", pendingPayroll, null, subtitle, CountStatus(pendingPayroll));
			}
		}
		catch
		{
			SetZoneError(summary, Actions, "HR action queue could not be loaded.");
		}
	}

	private async Task<int> CountPendingLeaveApprovalsAsync(UserScopeContext scope)
	{
		var employeeQuery = await hrEmployeeScopeReader.GetScopedEmployeeQueryAsync(scope);
		var activeEmployeeIds = await asyncExecuter.ToListAsync(employeeQuery
			.Where(e => e.EmployeeStatusType == EmployeeStatusType.Active)
			.Select(e => e.Id));

		if (activeEmployeeIds.Count == 0)
		{
			return 0;
		}

		var leaveQuery = await employeeLeaveRepository.GetQueryableAsync();
		return await asyncExecuter.CountAsync(leaveQuery
			.Where(l => l.Status == EmployeeLeaveStatusType.Pending && activeEmployeeIds.Contains(l.EmployeeId)));
	}

	private async Task AddFinanceActionsAsync(DashboardSummaryDto summary, DashboardDateRangeDto range)
	{
		if (summary.DashboardTemplate != FinanceHr)
		{
			return;
		}

		if (!await HasAnyPermissionAsync(WidgetDefinitions["finance-reconciliation"].RequiredPermissions))
		{
			return;
		}

		if (!Guid.TryParse(summary.AcademicYearId, out var academicYearId))
		{
			return;
		}

		try
		{
			var toDate = ParseNormalizedDate(range.To);
			var reconciliation = await reconciliationAppService.GetTabCountsAsync(academicYearId, toDate.Month, toDate.Year);
			var pending = reconciliation.TotalPending;
			var subtitle = BuildCountSubtitle(pending, "receipt awaiting reconciliation", "receipts awaiting reconciliation");
			UpdateWidget(summary.ActionItems, "finance-reconciliation", pending, null, subtitle, CountStatus(pending));
		}
		catch
		{
			SetZoneError(summary, Actions, "Finance reconciliation count could not be loaded.");
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
