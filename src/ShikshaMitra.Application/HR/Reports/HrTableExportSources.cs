using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Search;
using ShikshaMitra.HR.Employees.Onboarding;
using ShikshaMitra.HR.Employees.Settings;
using ShikshaMitra.HR.Employees.ICards;
using ShikshaMitra.HR.Employees.Documents;
using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;
using ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.ProfessionalManagement;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.AttendanceImports;
using ShikshaMitra.TenantSettings;
using Volo.Abp.Application.Dtos;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Reports;

public record HrTableExportPage(List<JsonElement> Rows, long Total, int? NextOffset = null);

// Each entry calls the owning read service, including its permission, actor and scope rules.
// Client input can select only fields already exposed by that table; it cannot select a service or join.
public class HrTableExportSources(IAbpLazyServiceProvider services) : ITransientDependency
{
	private List<Dictionary<string, object?>>? _capturedRows;
	private string? _capturedQuery;
	private readonly Dictionary<Guid, string?> _calendarNames = [];
	private readonly Dictionary<Guid, string?> _organizationNames = [];
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
	{
		Converters = { new JsonStringEnumConverter() }
	};
	private T Service<T>() where T : notnull => services.LazyGetRequiredService<T>();
	public static readonly IReadOnlyDictionary<string, string[]> Fields = new Dictionary<string, string[]>
	{
		["hr-generated-report"] = [],
		["hr-policy-versions"] = ["versionNumber", "changeAction", "creationTime", "changeReason"],
		["hr-policy-assignments"] = ["target", "sourceType", "organization", "payrollPolicyName", "attendancePolicyName", "leavePolicyName", "effective", "priority", "reason", "status"],
		["hr-performance-cycles"] = ["name", "periodStart", "periodEnd", "status", "assignedCount", "finalizedCount"],
		["hr-performance-templates"] = ["name", "boardType", "staffCategory", "sections", "status", "weight"],
		["hr-earning-components"] = ["earningComponentName", "description", "earningType", "calculationType", "amount", "customComponent.componentName", "visibleInPayslip"],
		["hr-deduction-components"] = ["deductionComponentName", "description", "deductionType", "calculationType", "amount", "customComponent.componentName", "visibleInPayslip"],
		["hr-attendance-import-rows"] = ["employeeName", "attendanceDate", "timeIn", "timeOut", "errors", "warnings", "status", "attendanceStatus"],
		["hr-attendance-imports"] = ["fileName", "payrollMonth", "importedAt", "importedByName", "totalRows", "validRows", "invalidRows", "status", "isBatchLocked"],
		["hr-shifts"] = ["shiftName", "startTime", "endTime"],
		["hr-document-settings"] = ["title", "documentSize", "documentTypesString"],
		["hr-employee-documents"] = ["title", "docType", "statusVal"],
		["hr-icard-templates"] = ["name", "organization", "orientation", "status"],
		["hr-self-leave-history"] = ["name", "employeeIdentifier", "submittedBy", "organization", "description", "remarks", "paidType", "appliedDate", "startDate", "endDate", "totalDays", "reason", "status"],
		["hr-pf-rules"] = ["name", "code", "employeePercentage", "employerPercentage", "contributionBasis", "wageCeiling", "effectiveFrom", "isActive"],
		["hr-professional-tax"] = ["jurisdictionCode", "effectiveFrom", "fromSalary", "toSalary", "taxAmount", "deductInCessationMonth"],
		["hr-salary-documents"] = [],
		["hr-generated-report-history"] = ["name", "status", "period", "capturedAt", "rowCount"],
		["employee-table"] = ["roleValue", "fullName", "dob", "genderValue", "doi", "email", "mobileNo", "isTeaching", "shiftName", "organization", "statusValue"],
		["employee-onboarding-table"] = ["candidate", "position", "status", "submittedAt", "tokenExpiresAt"],
		["hr-attendance-register"] = ["employeeName", "present", "exceptions", "employeeGroup", "department", "editability"],
		["hr-manual-attendance"] = ["employeeCode", "employeeName", "fullName", "attendanceDate", "attendanceStatus", "status", "inTime", "outTime", "remarks", "errors", "warnings"],
		["payroll-monthly-workbench-table"] = ["employeeName", "payableDays", "fullGrossSalary", "grossSalary", "additionalEarnings", "totalEarnings", "totalDeduction", "netSalary", "previousMonthNetPay", "netPayDelta", "issueFilter", "status"],
		["hr-payroll-registration-readiness"] = ["employeeCode", "employeeName", "applyPf", "applyEsic", "effectiveMonth"],
		["esi-employee-impact"] = ["employeeName", "coverageDecisionLabel", "enrollmentStatus", "statutoryWages", "employeeContribution", "employerContribution", "readiness"],
		["hr-policy-calendar"] = ["name", "effective", "weeklyRule", "holidayCount", "version", "updatedAt", "status"],
		["hr-policy-payroll"] = ["name", "calendar", "modeOrThresholds", "integration", "version", "updatedAt", "status"],
		["hr-policy-attendance"] = ["name", "calendar", "modeOrThresholds", "integration", "version", "updatedAt", "status"],
		["hr-policy-leave"] = ["policyTitle", "ruleCount", "buckets", "entitlements", "controls", "updatedAt", "status"],
		["hr-leave-requests"] = ["submittedBy", "appliedDate", "name", "employeeName", "employeeIdentifier", "organization", "description", "remarks", "leavePolicyName", "paidType", "startDate", "endDate", "totalDays", "status", "reason", "appliedOn"]
	};
	public static void Validate(HrTableExportRequest request)
	{
		if (request == null || request.Columns == null || request.Columns.Any(x => x == null)) throw HrReportingEngine.Invalid("Provide a table and its data columns.");
		if (!Fields.TryGetValue(request.Table, out var allowed)) throw HrReportingEngine.Invalid("Choose a supported HR table.");
		if (request.Format is not ("excel" or "pdf") || request.Columns.Count is < 1 or > 60 || request.Columns.Any(x => (request.Table is not ("hr-generated-report" or "hr-salary-documents") && !allowed.Contains(x.Id)) || string.IsNullOrWhiteSpace(x.Label) || x.Label.Length > 128)
			|| request.Columns.Select(x => x.Id).Distinct().Count() != request.Columns.Count) throw HrReportingEngine.Invalid("Choose supported data columns and Excel or PDF.");
		if (request.Query.ValueKind != JsonValueKind.Object || request.Query.GetRawText().Length > 32768) throw HrReportingEngine.Invalid("Provide a valid table query of at most 32 KB.");
	}
	public async Task EnsureAccessAsync(HrTableExportRequest request)
	{
		Validate(request);
		if (request.Table == "hr-generated-report") {
			var run = await CapturedRunAsync(request);
			_ = new HrReportingTableQuery(ReportingCriteria(request), run.Columns, run.Parameters.TimeZone);
			return;
		}
		await ReadAsync(request, 0, 1);
	}
	private async Task<HrReportRunDto> CapturedRunAsync(HrTableExportRequest request)
	{
		var id = request.Query.TryGetProperty("runId", out var runId) && runId.ValueKind == JsonValueKind.String && runId.TryGetGuid(out var parsedId) && parsedId != Guid.Empty ? parsedId : throw HrReportingEngine.Invalid("Choose a captured report before exporting.");
		var run = await Service<IHrReportingWorkspaceAppService>().GetAsync(id);
		if (run.IsExpired) throw new Volo.Abp.BusinessException(HRErrorCodes.ReportResultExpired);
		if (run.Status != "Completed") throw new Volo.Abp.BusinessException(HRErrorCodes.ReportExportNotReady);
		if (request.Columns.Any(x => !run.Columns.Any(c => c.Id == x.Id))) throw HrReportingEngine.Invalid("Choose columns present in this captured report.");
		return run;
	}
	public async Task<HrTableExportPage> ReadAsync(HrTableExportRequest request, int offset, int size)
	{
		Validate(request);
		// These legacy readers enforce view access in their HTTP controllers.
		// Reapply the exact screen policy because exports call the reader directly.
		var screenPermission = request.Table switch {
			"hr-earning-components" => ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.View,
			"hr-deduction-components" => ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.View,
			"hr-shifts" => ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_Setting_ShiftManagement.View,
			"hr-pf-rules" => ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsPfRule.View,
			"hr-professional-tax" => ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsProfessionalTax.View,
			_ => null };
		if (screenPermission != null) await Service<IAuthorizationService>().CheckAsync(screenPermission);
		if (request.Table is ("hr-attendance-imports" or "hr-attendance-import-rows") &&
			!await Service<IAuthorizationService>().IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_BiometricAttendance.View))
			await Service<IAuthorizationService>().CheckAsync(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_MarkAttendance.View);
		if (request.Table == "hr-generated-report")
		{
			var run = await CapturedRunAsync(request);
			var id = run.Id;
			var service = Service<IHrReportingWorkspaceAppService>();
			var criteria = ReportingCriteria(request);
			if (criteria.HasCriteria)
			{
				var key = request.Query.GetRawText();
				if (_capturedQuery != key) {
					_capturedRows = await service.GetExportRowsAsync(id, criteria);
					_capturedQuery = key;
				}
				return new(_capturedRows!.Skip(offset).Take(size).Select(x => JsonSerializer.SerializeToElement(x, Json)).ToList(), _capturedRows.Count);
			}
			var page = await service.GetRowsAsync(id, offset / size + 1, size, criteria);
			return new(page.Rows.Select(x => JsonSerializer.SerializeToElement(x, Json)).ToList(), page.TotalCount);
		}
		if (request.Table == "hr-generated-report-history")
		{
			var history = await Service<IHrReportingWorkspaceAppService>().GetRunsAsync(offset, size, ReportingCriteria(request));
			return new(history.Items.Select(x => JsonSerializer.SerializeToElement(x, Json)).ToList(), offset + size + (history.HasMore ? 1 : 0), offset + size);
		}
		var q = JsonNode.Parse(request.Query.GetRawText())!.AsObject();
		foreach (var entry in q.ToList())
			if (entry.Value is JsonValue value && value.TryGetValue<string>(out var text) && string.IsNullOrWhiteSpace(text)) q.Remove(entry.Key);
		if (request.Table is "hr-shifts" or "hr-professional-tax") q.Remove("sorting");
		q["skipCount"] = offset; q["maxResultCount"] = size; q["page"] = offset / size + 1; q["pageSize"] = size;
		T Input<T>()
		{
			try { return q.Deserialize<T>(Json) ?? throw HrReportingEngine.Invalid("Provide valid table filters."); }
			catch (JsonException) { throw HrReportingEngine.Invalid("One or more table filters have an invalid value."); }
		}
		Guid RequiredId(string key) => Guid.TryParse(q[key]?.ToString(), out var id) && id != Guid.Empty ? id : throw HrReportingEngine.Invalid($"Choose a valid {key} before exporting.");
		Guid? OptionalId(string key) => q[key] == null ? null : RequiredId(key);
		object response = request.Table switch
		{
			"hr-policy-versions" => await Service<IHrPolicyVersionAppService>().GetListAsync(q["policyType"]?.ToString() ?? "", RequiredId("policyId"), Input<PagedAndSortedResultRequestDto>(), q["search"]?.ToString(), q["action"]?.ToString()),
			"hr-policy-assignments" => await Service<IHrPolicyAssignmentAppService>().GetListAsync(Input<PolicyListRequestDto>()),
			"hr-performance-cycles" => await Service<IProfessionalManagementAppService>().GetReviewCyclesAsync(offset / size + 1, size, q["search"]?.ToString()),
			"hr-performance-templates" => await Service<IProfessionalManagementAppService>().GetTemplatesAsync(offset / size + 1, size, q["search"]?.ToString()),
			"hr-earning-components" => await Service<IEarningComponentAppService>().GetFilteredListAsync(offset, size, q["search"]?.ToString()),
			"hr-deduction-components" => await Service<IDeductionComponentAppService>().GetFilteredListAsync(offset, size, q["search"]?.ToString()),
			"hr-attendance-import-rows" => await Service<IAttendanceImportLogAppService>().GetRowsAsync(RequiredId("importId"), Input<GetAttendanceImportRowsInput>()),
			"hr-attendance-imports" => await Service<IAttendanceImportLogAppService>().GetListAsync(Input<GetAttendanceImportLogListInput>()),
			"hr-shifts" => await Service<IShiftManagementAppService>().GetListAsync(Input<PagedAndSortedResultRequestDto>()),
			"hr-document-settings" => await Service<IDocumentSettingAppService>().GetDocumentListAsync(offset / size + 1, size),
			"hr-employee-documents" => await Service<IEmployeeDocumentAppService>().GetChecklistAsync(RequiredId("employeeId"), offset / size + 1, size, q["search"]?.ToString()),
			"hr-icard-templates" => await Service<IEmployeeICardTemplateAppService>().GetListAsync(OptionalId("academicYearId"), OptionalId("mediumStageId"), offset / size + 1, size, q["search"]?.ToString()),
			"hr-self-leave-history" => await Service<ILeaveManagementAppService>().GetMyListAsync(Input<LeaveManagementGetListInput>()),
			"hr-pf-rules" => await Service<IProvidentFundAppService>().GetFilteredListAsync(offset, size, q["search"]?.ToString()),
			"hr-professional-tax" => await Service<IProfessionalTaxAppService>().GetListAsync(Input<PagedAndSortedResultRequestDto>()),
			"employee-table" => await Service<IEmployeeSearchAppService>().GetFilteredListAsync(Input<GetEmployeeListInput>()),
			"employee-onboarding-table" => await Service<IEmployeeApplicationAppService>().GetListAsync(Input<GetEmployeeApplicationListInput>()),
			"hr-attendance-register" => await Service<IAttendanceRegisterAppService>().GetGroupedAsync(Input<GetGroupedAttendanceRegisterInput>()),
			"hr-manual-attendance" => await Service<ITakeAttendanceAppService>().GetDayAsync(Input<GetTakeAttendanceDayInput>()),
			"payroll-monthly-workbench-table" => await Service<IPayrollPreviewRunAppService>().GetEmployeesAsync(RequiredId("runId"), new PayrollPreviewPageInput { Page = offset / size, PageSize = size, Search = q["search"]?.ToString() }),
			"hr-payroll-registration-readiness" => await Service<IEmployeeStatutoryProfileAppService>().GetReadinessAsync(q["month"]?.ToString(), offset / size + 1, size, q["search"]?.ToString()),
			"esi-employee-impact" => await ReadEsiAsync(q, offset, size),
			"hr-policy-calendar" => await Service<IWorkCalendarPolicyAppService>().GetListAsync(Input<PolicyListRequestDto>()),
			"hr-policy-payroll" => await Service<IPayrollPolicyAppService>().GetListAsync(Input<PolicyListRequestDto>()),
			"hr-policy-attendance" => await Service<IAttendancePolicyAppService>().GetListAsync(Input<PolicyListRequestDto>()),
			"hr-policy-leave" => await Service<ILeavePolicyAppService>().GetListByAcademicYearIdAsync(RequiredId("academicYearId"), offset / size + 1, size, q["search"]?.ToString(), q["archivedOnly"]?.GetValue<bool>()),
			"hr-leave-requests" => await Service<ILeaveManagementAppService>().GetListAsync(Input<LeaveManagementGetListInput>()),
			"hr-salary-documents" => await ReadSalaryAsync(request, q),
			_ => throw HrReportingEngine.Invalid("Choose a supported HR table.")
		};
		var root = JsonSerializer.SerializeToElement(response, Json);
		var items = root.TryGetProperty("items", out var found) ? found : root.TryGetProperty("employees", out found) ? found : root.TryGetProperty("rows", out found) ? found : default;
		if (items.ValueKind != JsonValueKind.Array) throw HrReportingEngine.Invalid("This table source did not return a complete page contract.");
		var rows = items.EnumerateArray().Select(x => x.Clone()).ToList();
		if (request.Table is "hr-policy-payroll" or "hr-policy-attendance" or "hr-icard-templates")
		{
			for (var index = 0; index < rows.Count; index++)
			{
				var node = JsonNode.Parse(rows[index].GetRawText())!.AsObject();
				if (request.Table == "hr-icard-templates")
				{
					if (Guid.TryParse(node["mediumStageId"]?.ToString(), out var organizationId)) {
						if (!_organizationNames.ContainsKey(organizationId)) _organizationNames[organizationId] = (await Service<IRepository<EducationMediumStage, Guid>>().FindAsync(organizationId))?.Name;
						node["organization"] = _organizationNames[organizationId] ?? "Unavailable organization";
					} else node["organization"] = "All organizations";
				}
				else if (Guid.TryParse(node["workCalendarPolicyId"]?.ToString(), out var calendarId)) {
					if (!_calendarNames.ContainsKey(calendarId)) _calendarNames[calendarId] = (await Service<IWorkCalendarPolicyAppService>().GetAsync(calendarId)).Name;
					node["workCalendarPolicyName"] = _calendarNames[calendarId];
				}
				rows[index] = JsonSerializer.SerializeToElement(node, Json);
			}
		}
		return new(rows, root.GetProperty("totalCount").GetInt64());
	}
	private async Task<object> ReadSalaryAsync(HrTableExportRequest request, JsonObject query)
	{
		var input = query.Deserialize<HrReportRequestDto>(Json)!;
		if (input.DataSource is not ("payroll_statement" or "payroll_register")) throw HrReportingEngine.Invalid("Choose a salary statement or payroll register.");
		var fields = HrReportCatalog.GetSource(input.DataSource).Fields;
		if (request.Columns.Any(x => !fields.Any(f => f.Id == x.Id))) throw HrReportingEngine.Invalid("Choose supported salary document columns.");
		input.Columns = request.Columns.Select(x => x.Id).Append("employeeCode").Distinct().ToList();
		return await Service<IHrReportAppService>().PreviewAsync(input);
	}
	private async Task<object> ReadEsiAsync(JsonObject query, int offset, int size)
	{
		query["page"] = offset / size;
		return await Service<IEsiStatutoryAppService>().PreviewAsync(query.Deserialize<StatutoryImpactPreviewRequestDto>(Json)!);
	}
	private static HrReportTableQueryDto ReportingCriteria(HrTableExportRequest request) =>
		request.Query.TryGetProperty("criteria", out var criteria) ? HrReportTableQueryDto.Parse(criteria.GetRawText()) : new();
	public async Task<List<HrReportFieldDto>> ColumnsAsync(HrTableExportRequest request)
	{
		if (request.Table == "hr-salary-documents")
		{
			var source = request.Query.GetProperty("dataSource").GetString()!;
			return request.Columns.Select(x => { var field = HrReportCatalog.GetSource(source).Fields.Single(f => f.Id == x.Id); return new HrReportFieldDto { Id = x.Id, Label = x.Label, Type = field.Type == "number" && !new[] { "casualLeaveDays", "paidLeaveDays", "unpaidLeaveDays", "payableDays" }.Contains(field.Id) ? "money" : field.Type }; }).ToList();
		}
		if (request.Table != "hr-generated-report") return request.Columns.Select(HrTableExportProjection.Field).ToList();
		var run = await Service<IHrReportingWorkspaceAppService>().GetAsync(request.Query.GetProperty("runId").GetGuid());
		return request.Columns.Select(x => new HrReportFieldDto { Id = x.Id, Label = x.Label, Type = run.Columns.Single(c => c.Id == x.Id).Type }).ToList();
	}
	public async Task<Dictionary<string, decimal?>> TotalsAsync(HrTableExportRequest request, HrReportingResult result)
	{
		if (request.Table == "hr-generated-report")
		{
			var criteria = ReportingCriteria(request);
			if (!string.IsNullOrWhiteSpace(criteria.Search) || criteria.Filters.Count > 0) return [];
			var original = await Service<IHrReportingWorkspaceAppService>().GetAsync(request.Query.GetProperty("runId").GetGuid());
			return original.Totals.Where(x => result.Columns.Any(c => c.Id == x.Key)).ToDictionary(x => x.Key, x => x.Value);
		}
		var sumAllNumbers = request.Table is "payroll-monthly-workbench-table" or "esi-employee-impact" or "hr-salary-documents" or "hr-attendance-register" or "hr-attendance-imports";
		if (!sumAllNumbers) return [];
		return result.Columns.Where(x => x.Type is "number" or "money").ToDictionary(x => x.Id, x => {
			var available = result.Rows.Select(row => row.GetValueOrDefault(x.Id)).Where(value => value != null).Select(value => decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? (decimal?)number : null).Where(value => value.HasValue).ToList();
			return available.Count > 0 ? available.Sum() : null;
		});
	}
	public async Task<(string Name, HrReportParametersDto Parameters, string Context)> PresentationAsync(HrTableExportRequest request, string timezone)
	{
		var query = request.Query;
		var name = request.Table switch { "employee-table" => "Staff directory", "employee-onboarding-table" => "Staff onboarding",
			"hr-attendance-register" => "Staff attendance register", "hr-manual-attendance" => "Daily attendance",
			"payroll-monthly-workbench-table" => "Payroll register", "hr-payroll-registration-readiness" => "Payroll registration readiness",
			"esi-employee-impact" => "ESIC staff register", "hr-policy-calendar" => "Work calendars", "hr-policy-payroll" => "Payroll policies",
			"hr-policy-attendance" => "Attendance policies", "hr-policy-leave" => "Leave policies", "hr-leave-requests" => "Leave requests",
			"hr-generated-report-history" => "Generated reports", "hr-salary-documents" => "Salary documents",
            "hr-policy-versions" => "Policy versions", "hr-policy-assignments" => "Policy assignments", "hr-performance-cycles" => "Performance review cycles", "hr-performance-templates" => "Performance templates",
            "hr-earning-components" => "Earning components", "hr-deduction-components" => "Deduction components", "hr-attendance-imports" => "Attendance imports",
            "hr-shifts" => "Staff shifts", "hr-pf-rules" => "Provident fund rules", "hr-professional-tax" => "Professional tax ranges", "hr-document-settings" => "Staff document types", "hr-employee-documents" => "Staff document checklist", "hr-attendance-import-rows" => "Attendance import review",
            "hr-icard-templates" => "Staff identity card templates", "hr-self-leave-history" => "My leave history", _ => "HR table" };
		var p = new HrReportParametersDto { TimeZone = timezone };
		if (query.TryGetProperty("academicYearId", out var year) && year.ValueKind == JsonValueKind.String && Guid.TryParse(year.GetString(), out var yearId)) p.AcademicYearId = yearId;
		if (request.Table == "hr-generated-report") { var original = await Service<IHrReportingWorkspaceAppService>().GetAsync(query.GetProperty("runId").GetGuid()); return (original.Name, original.Parameters, "Captured report rows matching table search and filters; visible ordered data columns. Original report totals are omitted for filtered exports."); }
		if (request.Table == "payroll-monthly-workbench-table")
		{
			var run = await Service<IPayrollPreviewRunAppService>().GetAsync(query.GetProperty("runId").GetGuid());
			if (DateOnly.TryParse(run.Month + "-01", out var month)) { p.FromDate = month; p.ToDate = month.AddMonths(1).AddDays(-1); }
		}
		if (request.Table == "hr-salary-documents" && query.TryGetProperty("filters", out var filters)) query = filters;
		if (query.TryGetProperty("employeeId", out var employee) && employee.ValueKind == JsonValueKind.String && Guid.TryParse(employee.GetString(), out var employeeId)) p.EmployeeIds = [employeeId];
		if (query.TryGetProperty("employeeIds", out var staff) && staff.ValueKind == JsonValueKind.Array) p.EmployeeIds = staff.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String && x.TryGetGuid(out var id) ? id : Guid.Empty).Where(x => x != Guid.Empty).ToList();
		if (query.TryGetProperty("departments", out var departments) && departments.ValueKind == JsonValueKind.Array) p.Departments = departments.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList();
		string? Text(string key) => query.TryGetProperty(key, out var value) ? value.ToString() : null;
		if (DateOnly.TryParse(Text("startDate") ?? Text("fromDate") ?? Text("attendanceDate"), out var from)) p.FromDate = from;
		if (DateOnly.TryParse(Text("endDate") ?? Text("toDate") ?? Text("attendanceDate"), out var to)) p.ToDate = request.Table == "hr-salary-documents" ? new DateOnly(to.Year, to.Month, 1).AddMonths(1).AddDays(-1) : to;
		if (DateOnly.TryParse(Text("month") + "-01", out var anchor)) { p.FromDate = anchor; p.ToDate = anchor.AddMonths(1).AddDays(-1); }
		if (DateTime.TryParseExact(Text("monthYear"), "MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var esi)) { p.FromDate = DateOnly.FromDateTime(esi); p.ToDate = p.FromDate.Value.AddMonths(1).AddDays(-1); }
		p.IncludeDrafts = Text("includeDrafts") == "True" || Text("includeDrafts") == "true";
		var context = new[] { "search", "status", "employeeGroup", "department", "coverage", "readiness", "exceptionFilter", "payrollState", "isArchived", "archivedOnly" }
			.Select(key => (Key: key, Value: Text(key))).Where(x => !string.IsNullOrWhiteSpace(x.Value)).Select(x => x.Key + ": " + x.Value);
		return (name, p, "Complete authorized table; " + string.Join("; ", context));
	}
}
