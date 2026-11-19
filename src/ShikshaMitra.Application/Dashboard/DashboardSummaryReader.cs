using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Validation;
using Volo.Abp.Users;
using static ShikshaMitra.Dashboard.DashboardSummaryMutator;
using static ShikshaMitra.Dashboard.DashboardTemplates;
using static ShikshaMitra.Dashboard.DashboardWidgetCatalog;
using static ShikshaMitra.Dashboard.DashboardZones;

namespace ShikshaMitra.Dashboard;

public class DashboardSummaryReader(
	DashboardSummarySlicePipeline slicePipeline,
	IPermissionChecker permissionChecker,
	ILogger<DashboardSummaryReader> logger,
	DashboardFocusReader focusReader,
	DashboardPersonalFocusReader personalReader,
	ICurrentUser currentUser)
	: ITransientDependency
{
	public async Task<DashboardSummaryDto> GetSummaryAsync(DashboardSummaryRequestDto? input, DateTime now)
	{
		input ??= new DashboardSummaryRequestDto();
		var template = NormalizeTemplate(input.DashboardTemplate);
		DashboardPresentationPolicy.Validate(template, input.PresentationRole);
		var range = NormalizeDateRange(input.From, input.To, now.Date);
		if (template == StudentParent)
			return await personalReader.ReadAsync(input, range, now);
		var summary = BuildBaseSummary(template, input.AcademicYearId, range, now);
		summary.PresentationRole = await focusReader.ResolvePresentationAsync(template, input.PresentationRole, currentUser.Roles);
		var sliceContext = new DashboardSummarySliceContext(summary, range);

		foreach (var registration in slicePipeline.Registrations)
		{
			await ApplySliceAsync(registration, sliceContext);
		}

		await FilterByPermissionsAsync(summary);
		await focusReader.ApplyAsync(summary);
		ApplySemanticMetadata(summary);

		return summary;
	}

	private async Task ApplySliceAsync(DashboardSummarySliceRegistration registration, DashboardSummarySliceContext context)
	{
		var existingZoneErrors = context.Summary.ZoneErrors.Keys.ToHashSet(StringComparer.Ordinal);
		var zoneSnapshot = DashboardSummarySnapshot.Capture(context.Summary);
		var stopwatch = Stopwatch.StartNew();
		try
		{
			await registration.Provider.ApplyAsync(context);
		}
		catch (Exception exception)
		{
			stopwatch.Stop();
			zoneSnapshot.Restore(context.Summary);
			SetZoneError(context.Summary, registration.AffectedZone, registration.FailureMessage);
			logger.LogError(
				exception,
				"Dashboard slice {SliceName} failed for template {DashboardTemplate} after {ElapsedMilliseconds}ms.",
				registration.Name,
				context.Summary.DashboardTemplate,
				stopwatch.ElapsedMilliseconds);
			return;
		}

		stopwatch.Stop();
		var newZoneErrors = context.Summary.ZoneErrors
			.Where(error => !existingZoneErrors.Contains(error.Key))
			.Select(error => $"{error.Key}: {error.Value}")
			.ToArray();

		if (newZoneErrors.Length > 0)
		{
			logger.LogWarning(
				"Dashboard slice {SliceName} completed for template {DashboardTemplate} in {ElapsedMilliseconds}ms with zone errors: {ZoneErrors}.",
				registration.Name,
				context.Summary.DashboardTemplate,
				stopwatch.ElapsedMilliseconds,
				string.Join("; ", newZoneErrors));
			return;
		}

		logger.LogDebug(
			"Dashboard slice {SliceName} completed for template {DashboardTemplate} in {ElapsedMilliseconds}ms.",
			registration.Name,
			context.Summary.DashboardTemplate,
			stopwatch.ElapsedMilliseconds);
	}

	private static string NormalizeTemplate(string? value)
	{
		var template = string.IsNullOrWhiteSpace(value) ? PrincipalAdmin : value.Trim();
		if (ValidTemplates.Contains(template))
		{
			return template;
		}

		throw new AbpValidationException(
			"Dashboard template is invalid.",
			[new ValidationResult("Dashboard template is invalid.", [nameof(DashboardSummaryRequestDto.DashboardTemplate)])]);
	}

	private static DashboardDateRangeDto NormalizeDateRange(string? fromValue, string? toValue, DateTime now)
	{
		var defaultFrom = new DateTime(now.Year, now.Month, 1);
		var from = ParseDateOrDefault(fromValue, defaultFrom, nameof(DashboardSummaryRequestDto.From));
		var to = ParseDateOrDefault(toValue, now, nameof(DashboardSummaryRequestDto.To));

		if (from > to || (to - from).TotalDays > 366)
		{
			throw new AbpValidationException(
				"Dashboard date range is invalid.",
				[new ValidationResult("Supply an ordered date range of at most 367 days.", [nameof(DashboardSummaryRequestDto.From), nameof(DashboardSummaryRequestDto.To)])]);
		}

		return new DashboardDateRangeDto
		{
			Label = from == to ? "Today" : "Month to date",
			From = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
			To = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
		};
	}

	private static DateTime ParseDateOrDefault(string? value, DateTime fallback, string memberName)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return fallback.Date;
		}

		if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
		{
			return parsed.Date;
		}

		throw new AbpValidationException(
			"Dashboard date is invalid.",
			[new ValidationResult("Date must be yyyy-MM-dd.", [memberName])]);
	}

	private static DashboardSummaryDto BuildBaseSummary(
		string template,
		string? academicYearId,
		DashboardDateRangeDto dateRange,
		DateTime generatedAt)
	{
		var summary = new DashboardSummaryDto
		{
			DashboardTemplate = template,
			AcademicYearId = string.IsNullOrWhiteSpace(academicYearId) ? null : academicYearId,
			DateRange = dateRange,
			ScopeLabel = ScopeLabelFor(template),
			GeneratedAtUtc = generatedAt.Kind == DateTimeKind.Utc
				? generatedAt
				: DateTime.SpecifyKind(generatedAt, DateTimeKind.Utc)
		};

		switch (template)
		{
			case Leadership:
				Add(summary.KpiCards, Widget("leadership-total-students", "Total Students", "Review", "Open scoped student records", "primary"));
				Add(summary.KpiCards, Widget("leadership-fee-collection", "Fee Collection", "Open", "Review collections in Finance", "warning"));
				Add(summary.KpiCards, Widget("leadership-compliance", "Compliance Readiness", "Review", "UDISE, APAAR, CBSE and safety readiness", "warning"));
				Add(summary.ActionItems, Widget("leadership-admission-approvals", "Admission approvals", null, "Open the admissions review workflow.", "info"));
				Add(summary.ActionItems, Widget("leadership-payroll-review", "Payroll review", null, "Open payroll readiness before lock.", "info"));
				Add(summary.RiskAlerts, Widget("leadership-certificate-risk", "Safety certificate readiness", null, "Review certificate expiry and disclosure gaps.", "warning"));
				Add(summary.ModuleSnapshots, Widget("leadership-academic-health", "Academic Health", "Open", "Academic readiness and intervention dashboard", "info"));
				Add(summary.ModuleSnapshots, Widget("leadership-transport-health", "Transport Health", "Open", "Route and safety exception dashboard", "info"));
				break;
			case PrincipalAdmin:
				Add(summary.KpiCards, Widget("principal-teacher-attendance", "Teacher Attendance", "Open", "Review staff attendance tracking", "info"));
				Add(summary.KpiCards, Widget("principal-curriculum", "Curriculum Completion", "Review", "Open chapter progress and syllabus updates", "warning"));
				Add(summary.KpiCards, Widget("principal-pending-admissions", "Admissions", "Review", "Open the admissions workflow.", "info"));
				Add(summary.ActionItems, Widget("principal-leave-approvals", "Staff leave approvals", null, "Open staff leave requests and approvals.", "info"));
				Add(summary.ActionItems, Widget("principal-transport-applications", "Transport applications", null, "Open the route allocation workflow.", "info"));
				Add(summary.RiskAlerts, Widget("principal-weak-classes", "Weak class performance", null, "Open Academic Dashboard for intervention planning.", "warning"));
				Add(summary.ModuleSnapshots, Widget("principal-academic-dashboard", "Academic Dashboard", "Open", "Class performance and syllabus risk", "info"));
				Add(summary.ModuleSnapshots, Widget("principal-security-dashboard", "Safety Exceptions", "Open", "Open security dashboard for visitor and gate-pass follow-up", "info"));
				break;
			case Teacher:
				Add(summary.KpiCards, Widget("teacher-classes", "Teacher timetable", "Open", "Use timetable for assigned classes", "info"));
				Add(summary.KpiCards, Widget("teacher-homework", "Pending Homework Review", "Review", "Homework and assignment feedback queue", "warning"));
				Add(summary.KpiCards, Widget("teacher-exam-evaluation", "Pending Exam Evaluation", "Review", "Marks entry and evaluation tasks", "info"));
				Add(summary.ActionItems, Widget("teacher-attendance", "Take class attendance", null, "Open attendance marking for assigned classes.", "info"));
				Add(summary.RiskAlerts, Widget("teacher-student-risk", "Students needing follow-up", null, "Use scoped student records for attendance or academic risk.", "warning"));
				Add(summary.ModuleSnapshots, Widget("teacher-class-performance", "Class Performance", "Open", "Academic dashboard and class trends", "info"));
				break;
			case FinanceHr:
				Add(summary.KpiCards, Widget("finance-collection", "Fee collection", "Open", "Open collections and receipts.", "info"));
				Add(summary.KpiCards, Widget("finance-dues", "Outstanding Dues", "Review", "Pending fee follow-up", "warning"));
				Add(summary.KpiCards, Widget("hr-payroll-readiness", "Payroll Readiness", "Review", "Payroll blockers and readiness", "info"));
				Add(summary.KpiCards, Widget("hr-staff-attendance", "Staff Attendance", "Open", "Attendance tracking and exceptions", "primary"));
				Add(summary.ActionItems, Widget("finance-reconciliation", "Fee reconciliation", null, "Open bank and receipt reconciliation.", "info"));
				Add(summary.ActionItems, Widget("hr-leave-approvals", "Leave approvals", null, "Open staff leave requests and approvals.", "info"));
				Add(summary.RiskAlerts, Widget("finance-concession-impact", "Concession impact review", null, "Review concessions before month close.", "warning"));
				Add(summary.ModuleSnapshots, Widget("finance-dashboard", "Finance Dashboard", "Open", "Collections, dues and concessions", "info"));
				Add(summary.ModuleSnapshots, Widget("hr-dashboard", "HR Dashboard", "Open", "Attendance, leave and payroll", "info"));
				break;
			case StudentParent:
				Add(summary.KpiCards, Widget("student-attendance", "Attendance", "View", "Open student dashboard for personal attendance.", "success"));
				Add(summary.KpiCards, Widget("student-assignments", "Assignments Due", "View", "Open homework and learning tasks", "warning"));
				Add(summary.KpiCards, Widget("student-fee-status", "Fee Status", "Review", "Open fee collection for dues", "info"));
				Add(summary.ActionItems, Widget("student-document-update", "Student documents", null, "Open student identity documents.", "info"));
				Add(summary.ModuleSnapshots, Widget("student-academic-summary", "Academic Summary", "View", "Homework, exams and progress", "info"));
				break;
		}

		AddSharedCalendarAndAnnouncements(summary);

		return summary;
	}

	private static string ScopeLabelFor(string template) => template switch
	{
		Leadership => "Your permitted school scope",
		PrincipalAdmin => "Your permitted operational scope",
		Teacher => "Your assigned classes",
		FinanceHr => "Your permitted finance and staff scope",
		StudentParent => "Your linked student",
		_ => "Your permitted scope"
	};

	private static void ApplySemanticMetadata(DashboardSummaryDto summary)
	{
		ApplyZoneSemantics(summary.KpiCards, "metric", "View details");
		ApplyZoneSemantics(summary.ActionItems, "action", "Review action");
		ApplyZoneSemantics(summary.RiskAlerts, "risk", "Review risk");
		ApplyZoneSemantics(summary.ModuleSnapshots, "health", "Open dashboard");
		ApplyZoneSemantics(summary.CalendarItems, "timeline", "View event");
		ApplyZoneSemantics(summary.Announcements, "announcement", "Read update");
		summary.DataState = summary.ZoneErrors.Count == 0 ? "live" : "partial";
	}

	private static void ApplyZoneSemantics(
		IEnumerable<DashboardWidgetDto> widgets,
		string kind,
		string actionLabel)
	{
		foreach (var widget in widgets)
		{
			widget.Kind = kind;
			widget.ActionLabel = actionLabel;
			widget.Priority = ResolvePriority(widget, kind);
		}
	}

	private static string ResolvePriority(DashboardWidgetDto widget, string kind)
	{
		if (string.Equals(widget.Status, "error", StringComparison.OrdinalIgnoreCase))
		{
			return "critical";
		}

		if (kind is "action" or "risk" && (widget.Count.GetValueOrDefault() > 0 ||
		    string.Equals(widget.Status, "warning", StringComparison.OrdinalIgnoreCase)))
		{
			return "high";
		}

		return string.Equals(widget.Status, "success", StringComparison.OrdinalIgnoreCase)
			? "low"
			: "normal";
	}

	private async Task FilterByPermissionsAsync(DashboardSummaryDto summary)
	{
		summary.KpiCards = await FilterWidgetsAsync(summary.KpiCards, Kpis);
		summary.ActionItems = await FilterWidgetsAsync(summary.ActionItems, Actions);
		summary.RiskAlerts = await FilterWidgetsAsync(summary.RiskAlerts, Risks);
		summary.CalendarItems = await FilterWidgetsAsync(summary.CalendarItems, DashboardZones.Calendar);
		summary.ModuleSnapshots = await FilterWidgetsAsync(summary.ModuleSnapshots, Modules);
		summary.Announcements = await FilterWidgetsAsync(summary.Announcements, Announcements);
	}

	private async Task<List<DashboardWidgetDto>> FilterWidgetsAsync(
		IEnumerable<DashboardWidgetDto> widgets,
		string zone)
	{
		var filtered = new List<DashboardWidgetDto>();
		foreach (var widget in widgets)
		{
			if (!WidgetDefinitions.TryGetValue(widget.Id, out var definition) || definition.Zone != zone)
			{
				continue;
			}

			if (!await HasAllPermissionsAsync(definition.RequiredPermissions))
			{
				continue;
			}

			widget.Owner = definition.Owner;
			widget.RequiredPermissions = definition.RequiredPermissions.ToList();
			widget.DrilldownUrl = definition.DrilldownUrl;
			filtered.Add(widget);
		}

		return filtered;
	}

	private async Task<bool> HasAllPermissionsAsync(IEnumerable<string> permissionNames)
	{
		var hasRequirement = false;
		foreach (var permissionName in permissionNames)
		{
			hasRequirement = true;
			if (!await permissionChecker.IsGrantedAsync(permissionName)) return false;
		}
		return hasRequirement;
	}

	private static void AddSharedCalendarAndAnnouncements(DashboardSummaryDto summary)
	{
		Add(summary.CalendarItems, Widget("calendar-periodic-test", "Exam schedules", null, "Open the exam schedule to review academic dates.", "info"));
		Add(summary.CalendarItems, Widget("calendar-safety-drill", "School calendar", null, "Open the calendar to review events and holidays.", "info"));
		Add(summary.Announcements, Widget("announcement-board-practical", "Published exam information", null, "Open exam schedules to review available information.", "info"));
		Add(summary.Announcements, Widget("announcement-ptm", "Communication updates", null, "Open Communication to review school updates.", "info"));
	}

	private sealed class DashboardSummarySnapshot
	{
		private DashboardSummarySnapshot(DashboardSummaryDto summary)
		{
			KpiCards = CloneWidgets(summary.KpiCards);
			ActionItems = CloneWidgets(summary.ActionItems);
			RiskAlerts = CloneWidgets(summary.RiskAlerts);
			CalendarItems = CloneWidgets(summary.CalendarItems);
			ModuleSnapshots = CloneWidgets(summary.ModuleSnapshots);
			Announcements = CloneWidgets(summary.Announcements);
			ZoneErrors = new Dictionary<string, string>(summary.ZoneErrors, StringComparer.Ordinal);
		}

		private List<DashboardWidgetDto> KpiCards { get; }
		private List<DashboardWidgetDto> ActionItems { get; }
		private List<DashboardWidgetDto> RiskAlerts { get; }
		private List<DashboardWidgetDto> CalendarItems { get; }
		private List<DashboardWidgetDto> ModuleSnapshots { get; }
		private List<DashboardWidgetDto> Announcements { get; }
		private Dictionary<string, string> ZoneErrors { get; }

		public static DashboardSummarySnapshot Capture(DashboardSummaryDto summary) => new(summary);

		public void Restore(DashboardSummaryDto summary)
		{
			summary.KpiCards = CloneWidgets(KpiCards);
			summary.ActionItems = CloneWidgets(ActionItems);
			summary.RiskAlerts = CloneWidgets(RiskAlerts);
			summary.CalendarItems = CloneWidgets(CalendarItems);
			summary.ModuleSnapshots = CloneWidgets(ModuleSnapshots);
			summary.Announcements = CloneWidgets(Announcements);
			summary.ZoneErrors = new Dictionary<string, string>(ZoneErrors, StringComparer.Ordinal);
		}

		private static List<DashboardWidgetDto> CloneWidgets(IEnumerable<DashboardWidgetDto> widgets) =>
			widgets.Select(widget => new DashboardWidgetDto
			{
				Id = widget.Id,
				RequiredPermissions = widget.RequiredPermissions.ToList(),
				Title = widget.Title,
				Value = widget.Value,
				Count = widget.Count,
				DueLabel = widget.DueLabel,
				Subtitle = widget.Subtitle,
				Status = widget.Status,
				Trend = widget.Trend,
				Owner = widget.Owner,
				DrilldownUrl = widget.DrilldownUrl,
				Health = widget.Health,
				Kind = widget.Kind,
				Priority = widget.Priority,
				ActionLabel = widget.ActionLabel
			}).ToList();
	}
}
