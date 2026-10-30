using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using P = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions;

namespace ShikshaMitra.Dashboard;

public class DashboardFocusReader(IPermissionChecker permissions, DashboardCollectionsFocusReader collections,
	DashboardOperationalFocusReader operations, DashboardSavedPayrollReader payroll, ILogger<DashboardFocusReader> logger) : ITransientDependency
{
	public async Task<string> ResolvePresentationAsync(string template, string? requested, IEnumerable<string> roles) =>
		DashboardPresentationPolicy.ResolveStaff(template, requested, roles,
			await permissions.IsGrantedAsync(P.Finance.Fees_FeeCollections.View),
			await permissions.IsGrantedAsync(P.HR.PayrollManagement_Dashboard.View));

	public async Task ApplyAsync(DashboardSummaryDto summary)
	{
		var focus = summary.PresentationRole switch
		{
			"leadership" or "finance" or "financeHr" => New("collections", summary.DashboardTemplate == "leadership" ? "leadership-fee-collection" : "finance-collection", "Fee collections", "Canonical receipt fee amounts in your permitted scope.", "INR", P.Finance.Fees_FeeCollections.View),
			"principal" => New("attendance", "principal-student-attendance", "Student attendance", "Policy-based attendance by class in your permitted scope.", "percent", P.Students.Attendance_MarkAttendance.View, P.Students.Records_StudentList.View),
			"admissions" => New("pipeline", "principal-pending-admissions", "Admissions workload", "Current application stages in your permitted academic scope.", "count", P.Students.AdmissionApplications.View),
			"teacher" => New("timetable", "teacher-classes", "Published weekly timetable", "Your published weekly lessons. Calendar closures and substitutions are available in Timetable.", "count", P.Academics.Timetable_TimetableManagement.View),
			"hr" => New("payroll", "hr-payroll-readiness", "Saved payroll review", "Saved preview evidence. Open Payroll to recalculate current readiness.", "count", P.HR.PayrollManagement_Dashboard.View),
			_ => null
		};
		if (focus == null) return;
		foreach (var permission in focus.RequiredPermissions)
			if (!await permissions.IsGrantedAsync(permission)) return;
		summary.Focus = focus;
		if (!Guid.TryParse(summary.AcademicYearId, out var year) || year == Guid.Empty)
		{
			focus.Message = "Select an academic year to load this overview.";
			return;
		}
		try
		{
			switch (focus.Kind)
			{
				case "collections": await collections.FillAsync(focus, summary, year); break;
				case "attendance": await operations.FillAttendanceAsync(focus, summary, year); break;
				case "pipeline": await operations.FillAdmissionsAsync(focus, year); break;
				case "timetable": await operations.FillTimetableAsync(focus, year); break;
				case "payroll":
					var saved = await payroll.ReadAsync(summary.DateRange.To![..7], year);
					focus.PeriodLabel = saved.Month;
					focus.AsOfUtc = saved.AsOfUtc;
					if (saved.Snapshots == 0) { focus.Message = "No saved preview for this month. Calculate readiness in Payroll."; break; }
					focus.Status = "ready";
					focus.Metrics = [new("employees", "Eligible employees", saved.Employees, "count"), new("saved", "Saved preview rows", saved.Snapshots, "count"), new("reviewed", "Reviewed", saved.Reviewed, "count"), new("locked", "Locked", saved.Locked, "count"), new("blocked", "Saved rows with blockers", saved.Blocked, "count")];
					focus.Series = [new("review", "Saved review stages", [new("pending", "Pending review", Math.Max(0, saved.Snapshots - saved.Reviewed - saved.Locked)), new("reviewed", "Reviewed", saved.Reviewed), new("locked", "Locked", saved.Locked)])];
					break;
			}
		}
		catch (Exception error)
		{
			logger.LogWarning(error, "Dashboard focus {FocusKind} could not be loaded.", focus.Kind);
			focus.Status = "error";
			focus.Message = "This overview could not be loaded. Try again.";
			focus.Series = []; focus.Items = []; focus.Metrics = [];
			summary.ZoneErrors["focus"] = focus.Message;
		}
	}

	private static DashboardFocusDto New(string kind, string widget, string title, string description, string unit, params string[] permissions) =>
		new() { Kind = kind, WidgetId = widget, Title = title, Description = description, Unit = unit, RequiredPermissions = permissions.ToList() };
}
