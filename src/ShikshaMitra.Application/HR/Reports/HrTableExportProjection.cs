using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.HR.Reports;

public static class HrTableExportProjection
{
	public static object? QueryValue(string table, HrReportFieldDto field, JsonElement exported, JsonElement raw, DateOnly today)
	{
		var display = Value(table, field.Id, exported, today);
		if (field.Id == "fullName" && table is ("employee-table" or "employee-icard-selection-table"))
			return new HrOperationalTextValue(display, exported.TryGetProperty("fullName", out var name) ? name.GetString() : null);
		var primary = (table, field.Id) switch {
			("employee-onboarding-table", "candidate") => "fullName",
			("employee-onboarding-table", "position") => "positionAppliedFor",
			("hr-manual-attendance", "fullName") => "fullName",
			("hr-leave-requests" or "hr-self-leave-history", "name") => "name",
			("hr-attendance-register" or "payroll-monthly-workbench-table" or "esi-employee-impact", "employeeName") => "employeeName",
			("hr-policy-calendar" or "hr-policy-assignments", "effective") => "effectiveFrom",
			_ => null
		};
		if (primary != null) return new HrOperationalTextValue(display, exported.TryGetProperty(primary, out var order) ? order.ToString() : null);
		if (field.Type != "enum" || display == null) return display;
		// Derived status labels may intentionally distinguish values with the same native code.
		if ((table == "employee-table" && field.Id == "statusValue") || (table == "hr-manual-attendance" && field.Id is ("status" or "attendanceStatus")) || (table == "hr-attendance-import-rows" && field.Id == "attendanceStatus")) return display;
		var key = (table, field.Id) switch {
			("employee-table", "genderValue") => "gender",
			("hr-earning-components", "earningType") or ("hr-deduction-components", "deductionType") => "componentType",
			("hr-policy-assignments", "sourceType") => "targetType",
			("hr-policy-assignments", "status") => "isActive",
			("hr-attendance-import-rows", "attendanceStatus") => "statusCode",
			_ => field.Id
		};
		return raw.TryGetProperty(key, out var native) && native.ValueKind is JsonValueKind.Number or JsonValueKind.String or JsonValueKind.True or JsonValueKind.False
			? new HrOperationalEnumValue(display, native.Clone()) : display;
	}
	public static object? Value(string table, string field, JsonElement row, DateOnly? schoolToday = null)
	{
		object? Get(string path)
		{
			var value = row;
			foreach (var part in path.Split('.')) if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(part, out value)) return null;
			return value.ValueKind switch { JsonValueKind.String => value.GetString(), JsonValueKind.Number => value.GetDecimal(), JsonValueKind.True => true, JsonValueKind.False => false, JsonValueKind.Null => null, _ => value.ToString() };
		}
		string Text(string name) => Convert.ToString(Get(name)) ?? "";
		string Join(params string[] values) => string.Join(" ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
		if (field == "fullName" && table is ("employee-table" or "employee-icard-selection-table"))
			return string.Join(" ", new[] { Text("fullName"), Text("employeeIdentifier") }.Where(value => !string.IsNullOrWhiteSpace(value)));
		if (table == "employee-icard-selection-table" && field == "readiness") {
			var missing = new[] { ("thumbnailPicture", "Photo"), ("mobileNo", "Phone"), ("designation", "Role"), ("employeeIdentifier", "Code") }
				.Where(item => string.IsNullOrWhiteSpace(Text(item.Item1))).Select(item => item.Item2).ToList();
			return missing.Count == 0 ? "Ready" : string.Join(", ", missing);
		}
		if (table == "hr-icard-templates" && field == "status") return Get("isActive") is true ? "Active" : "Inactive";
		if (table == "hr-attendance-imports" && field == "isBatchLocked") return Get(field) is true ? "Locked" : "Unlocked";
		if (table == "hr-document-settings" && field == "documentTypesString") return row.TryGetProperty("documentTypes", out var formats)
			? string.Join(", ", formats.EnumerateArray().Select(x => x.ToString() switch { "0" => "Jpg", "1" => "Jpeg", "2" => "Png", "3" => "Docx", "4" => "Pdf", "5" => "Xlsx", _ => x.ToString() })) : null;
		if (table == "hr-pf-rules") return field switch {
			"contributionBasis" => Text(field) == "BasicAndDearnessAllowance" ? "Basic + DA" : "Basic",
			"wageCeiling" => Get(field) ?? "No ceiling", "isActive" => Get(field) is true ? "Active" : "Inactive", _ => Get(field) };
		if (table == "hr-professional-tax") return field switch {
			"toSalary" => Get(field) ?? "No upper limit", "deductInCessationMonth" => Get(field) is true ? "Deduct" : "Do not deduct", _ => Get(field) };
		if (table is "hr-earning-components" or "hr-deduction-components") return field switch {
			"earningComponentName" or "deductionComponentName" => Get("name"),
			"earningType" or "deductionType" => Text("componentType").ToUpperInvariant(),
			"calculationType" => Text(field).ToUpperInvariant(),
			"amount" => Text("calculationType") == "Percentage" ? Get("percentage") : Get("amount"),
			"customComponent.componentName" => Text("dependOn") == "Gross" ? "Gross Salary" : Get("selectedEarningComponentName") ?? Get("selectedDeductionComponentName"),
			"visibleInPayslip" => Get(field) is true ? "Yes" : "No",
			_ => Get(field) };
		if (table == "hr-policy-assignments") return field switch {
			"target" => Get("targetDisplayName") ?? Get("targetKey"), "sourceType" => Text("targetType") == "employee" ? "Staff" : "Group",
			"organization" => row.TryGetProperty("targetOrganizationNames", out var organizations) ? string.Join(", ", organizations.EnumerateArray().Select(x => x.GetString())) : null,
			"effective" => EffectiveRange(Text("effectiveFrom"), Text("effectiveTo")), "status" => Get("isActive") is true ? "Active" : "Inactive",
			"reason" => Get("assignmentReason"), _ => Get(field) };
		if (table == "hr-policy-versions" && field == "changeReason") return Get("changeReason") ?? Get("summary");
		if (table == "hr-performance-templates") return field switch {
			"sections" => row.TryGetProperty("sections", out var sections) ? string.Join("; ", sections.EnumerateArray().Select(x => x.GetProperty("name").GetString() + ": " + x.GetProperty("weight") + "% " + SqaaDomain(x))) : null,
			"weight" => row.TryGetProperty("sections", out var weights) ? weights.EnumerateArray().Sum(x => x.GetProperty("weight").GetDecimal()) : null,
			_ => Get(field) };
		if (table == "hr-generated-report-history") return field switch {
			"period" => string.IsNullOrEmpty(Text("parameters.fromDate")) ? "Current records" : $"{Text("parameters.fromDate")} to {Text("parameters.toDate")}",
			"status" => Get("isExpired") is true ? "Expired" : Get("status"), _ => Get(field) };
		if (table == "employee-table") return field switch
		{
			"isTeaching" => Get(field) is true ? "Teaching" : "Non-teaching",
			"roleValue" => Get("roleName"), "genderValue" => Get("gender"), "doi" => Get("doj"),
			"shiftName" => Get("shift.name"), "organization" => row.TryGetProperty("organizationNames", out var names) ? string.Join(", ", names.EnumerateArray().Select(x => x.GetString())) : null,
			"statusValue" => Text("status") switch { "0" when Text("doj").Length >= 10 && DateOnly.TryParse(Text("doj")[..10], out var joining) && schoolToday.HasValue && joining > schoolToday => "Upcoming", "0" => "Active", "1" => "Inactive", "2" => "Left", "3" => "Retired", _ => Text("status") }, _ => Get(field)
		};
		if (table == "employee-onboarding-table") return field switch {
			"candidate" => Join(string.IsNullOrEmpty(Text("fullName")) ? "Candidate" : Text("fullName"), new[] { Text("email"), Text("mobileNo"), Text("applicationNumber") }.FirstOrDefault(value => !string.IsNullOrEmpty(value)) ?? ""),
			"position" => Join(Text("positionAppliedFor"), Text("departmentAppliedFor")), _ => Get(field) };
		if (table == "hr-attendance-register") return field switch
		{
			"employeeName" => Join(Text("employeeName"), Text("employeeCode"), Text("educationMediumStageName")),
			"exceptions" => string.Join("; ", new[] { ("absent", "Absent"), ("halfDay", "Half day"), ("missingPunch", "Missing"), ("locked", "Locked") }
				.Where(item => Get(item.Item1) is decimal count && count > 0).Select(item => $"{item.Item2} {Text(item.Item1)}")
				.Concat(Get("hasExceptions") is true ? [] : new[] { "Clear" })),
			"editability" => $"{Text("editable")} editable" + (Get("locked") is decimal locked && locked > 0 ? $"; {Text("locked")} locked" : ""), _ => Get(field)
		};
		if (table == "hr-payroll-registration-readiness") return field switch
		{
			"applyPf" => Get("applyPf") is true ? Get("pfNumberPresent") is true ? "Number recorded" : "Number missing" : "Turned off",
			"applyEsic" => Get("applyEsic") is true ? Get("esicNumberPresent") is true ? "Number recorded" : "Number missing" : "Turned off", _ => Get(field)
		};
		if (table == "payroll-monthly-workbench-table") return field switch {
			"employeeName" => Join(Text("employeeName"), Text("employeeIdentifier"), Text("employeeGroup")),
			"netSalary" => Get("netPay"),
			"status" => Get("isLocked") is true ? "LOCKED" : Get("reviewStatus"),
			"issueFilter" => PayrollIssues(row), _ => Get(field) };
		if (table is "hr-leave-requests" or "hr-self-leave-history") return field switch { "name" => Join(Text("name"), Text("employeeIdentifier")), "submittedBy" => Get("submittedByName"), "employeeName" => Get("name"), "employeeIdentifier" => Get("employeeIdentifier"), "organization" => Get("mediumStageName"), "reason" => Get("description"), _ => Get(field) };
		if (table == "hr-manual-attendance") return field switch {
			"fullName" => Join(Text("fullName"), Text("employeeCode")),
			"employeeName" => Get("fullName"), "attendanceDate" => Get("dateIso"), "timeIn" => Get("inTime"), "timeOut" => Get("outTime"),
			"status" or "attendanceStatus" => Get("isHoliday") is true ? "Holiday" : Text("status") is "NotMarked" or "0" ? "Pending" : Get("status"),
			"errors" or "warnings" => row.TryGetProperty(field, out var values) ? string.Join("; ", values.EnumerateArray().Select(x => x.GetString())) : null, _ => Get(field) };
		if (table == "esi-employee-impact" && field == "employeeName") return Join(Text("employeeName"), string.IsNullOrEmpty(Text("employeeIdentifier")) ? "No staff code" : Text("employeeIdentifier"));
		if (table == "esi-employee-impact" && field == "readiness") return row.TryGetProperty("issues", out var issues) && issues.GetArrayLength() > 0
			? string.Join("; ", issues.EnumerateArray().Select(x => x.GetProperty("title").GetString())) : Get("isCovered") is true ? "Ready" : "No action needed";
		if (table == "hr-policy-leave" && field is "buckets" or "entitlements" or "controls")
		{
			var rules = row.TryGetProperty("rules", out var allRules) ? allRules.EnumerateArray().Where(x => !x.TryGetProperty("isActive", out var active) || active.ValueKind != JsonValueKind.False).ToList() : [];
			if (field == "controls")
			{
				var flags = new[] { ("requiresApproval", "Approval"), ("carryForward", "Carry forward"), ("encashmentAllowed", "Encashment") }
					.Where(flag => rules.Any(rule => rule.TryGetProperty(flag.Item1, out var enabled) && enabled.ValueKind == JsonValueKind.True)).Select(flag => flag.Item2).ToList();
				return flags.Count > 0 ? string.Join("; ", flags) : "Basic";
			}
			return string.Join("; ", new[] { "Paid", "Unpaid" }.Select(bucket => {
				var rule = rules.FirstOrDefault(x => x.GetProperty("paidType").GetString() == bucket);
				if (rule.ValueKind != JsonValueKind.Object) return bucket + ": -";
				var unlimited = (rule.TryGetProperty("isUnlimited", out var free) && free.ValueKind == JsonValueKind.True) || rule.GetProperty("entitlementMode").GetString() == "Unlimited";
				return bucket + ": " + (unlimited ? "Unlimited" : rule.GetProperty("leaveCount") + " " + rule.GetProperty("accrualPolicyType").GetString());
			}));
		}
		if (table.StartsWith("hr-policy-", StringComparison.Ordinal)) return field switch
		{
			"policyTitle" => Get("policyTitle") ?? Get("name") ?? Get("policyName"),
			"effective" => EffectiveRange(Text("effectiveFrom"), Text("effectiveTo")),
			"status" => Get("status"),
			"weeklyRule" => CalendarWeeklyRule(row),
			"holidayCount" => Get("holidaySummary.includedCount"),
			"updatedAt" => Get("lastModificationTime") ?? Get("creationTime"),
			"version" => Get("versionNumber") ?? Get("version"),
			"calendar" => Get("workCalendarPolicyName"),
			"integration" => "Per staff attendance",
			"modeOrThresholds" => table == "hr-policy-payroll" ? $"{Text("dayCalculationType")} / {Text("saturdayWorkPolicy")}" : Text("captureMode") == "manual_status" ? "Manual status" : $"{(Text("captureMode") == "manual_with_time" ? "Manual with time" : "Integrated")} / {Text("fullDayThresholdPercentage")}%",
			_ => Get(field)
		};
		if (table == "hr-attendance-import-rows") return field is "errors" or "warnings" && row.TryGetProperty(field, out var messages) ? string.Join("; ", messages.EnumerateArray().Select(x => x.GetString())) : field == "attendanceStatus" ? ImportAttendanceStatus(row) : Get(field);
		return Get(field);
	}
	private static string EffectiveRange(string from, string to)
	{
		static string Date(string value) => value.Length >= 10 && DateOnly.TryParseExact(value[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) : "-";
		return $"{Date(from)} - {(string.IsNullOrEmpty(to) ? "No end date" : Date(to))}";
	}
	private static string CalendarWeeklyRule(JsonElement row)
	{
		var pattern = row.TryGetProperty("weeklyOffPattern", out var value) ? value.GetString() : null;
		if (string.IsNullOrEmpty(pattern) || pattern == "saturday_sunday") return "Saturday + Sunday Off";
		if (pattern is "sunday" or "sunday_only") return "Sunday Off";
		string[] days = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
		var selected = row.TryGetProperty("customWeeklyOffs", out var custom) && custom.ValueKind == JsonValueKind.Array ? custom.EnumerateArray().Select(day => day.GetInt32()).Where(day => day >= 0 && day <= 6).Distinct().OrderBy(day => day).Select(day => days[day]).ToList() : [];
		return "Custom: " + (selected.Count == 0 ? "None" : string.Join(", ", selected));
	}
	private static string SqaaDomain(JsonElement section)
	{
		var domain = section.TryGetProperty("sqaaDomain", out var value) ? value.ToString() : "2";
		return domain switch { "0" or "CurriculumPedagogyAssessment" => "Curriculum, Pedagogy & Assessment", "1" or "Infrastructure" => "Infrastructure", "2" or "HumanResources" => "Human Resources", "3" or "LeadershipGovernance" => "Leadership & Governance", "4" or "InclusivenessWellBeing" => "Inclusiveness and Well-being", "5" or "BeneficiarySatisfaction" => "Beneficiary Satisfaction", "6" or "ManagementSchoolProcesses" => "Management and School Processes", _ => "SQAAF" };
	}
	private static string ImportAttendanceStatus(JsonElement row)
	{
		if (row.TryGetProperty("statusCode", out var code)) {
			var status = code.ToString();
			if (status is "0" or "Present") return "Present";
			if (status is "1" or "Absent") return "Absent";
			if (status is "2" or "HalfDay") return "HalfDay";
		}
		var text = row.TryGetProperty("status", out var value) ? value.ToString() : "";
		var normalized = string.Concat(text.Where(character => !char.IsWhiteSpace(character) && character is not '_' and not '-')).ToUpperInvariant();
		return normalized switch { "PRESENT" => "Present", "ABSENT" or "FULLDAYLEAVE" => "Absent", "HALFDAY" or "HALFDAYLEAVE" => "HalfDay", _ => "Pending" };
	}
	private static string PayrollIssues(JsonElement row)
	{
		var codes = row.TryGetProperty("issueCodes", out var issues) ? issues.EnumerateArray().Select(issue => issue.GetString() ?? "").ToList() : [];
		if (codes.Count == 0) return "CLEAN";
		// Presentation categories already used by the payroll workbench, not new lock rules.
		string[] critical = ["ESI_COMPONENT_REQUIRED", "PF_MEMBERSHIP_REVIEW_REQUIRED", "PF_ENROLLMENT_REQUIRED", "PF_CONSENT_REQUIRED", "PF_ESTABLISHMENT_NOT_COVERED", "ESI_BASIC_COMPONENT_REQUIRED", "PF_WAGE_BASIS_REQUIRED", "MISSING_SALARY_STRUCTURE", "NO_ATTENDANCE", "NEGATIVE_NET_PAY", "PAYROLL_POLICY_CONFIGURATION", "ESI_ESTABLISHMENT_INACTIVE", "ESI_WAGE_CLASSIFICATION_REQUIRED", "ESI_ENROLLMENT_REVIEW_REQUIRED", "COMPENSATION_REVISION_REQUIRED", "COMPENSATION_TARGET_VARIANCE"];
		return codes.Any(code => Enumerable.Contains(critical, code)) ? "CRITICAL" : "HAS_ISSUES";
	}
	public static HrReportFieldDto Field(HrTableExportColumn column)
	{
		var money = new[] { "fullGrossSalary", "grossSalary", "additionalEarnings", "totalEarnings", "totalDeduction", "netSalary", "previousMonthNetPay", "netPayDelta", "statutoryWages", "employeeContribution", "employerContribution", "wageCeiling", "fromSalary", "toSalary", "taxAmount" };
		var number = new[] { "rowNumber", "employeePercentage", "employerPercentage", "present", "payableDays", "holidayCount", "version", "versionNumber", "ruleCount", "totalDays", "rowCount", "assignedCount", "finalizedCount", "weight", "priority", "documentSize", "totalRows", "validRows", "invalidRows", "amount" };
		return new() { Id = column.Id, Label = column.Label, Type = money.Contains(column.Id) ? "money" : number.Contains(column.Id) ? "number" : column.Id is "dob" or "doi" or "startDate" or "endDate" or "attendanceDate" or "periodStart" or "periodEnd" or "effectiveFrom" ? "date" : column.Id is "submittedAt" or "tokenExpiresAt" or "updatedAt" or "appliedOn" or "creationTime" or "capturedAt" or "importedAt" or "appliedDate" ? "datetime" : "string" };
	}
}
