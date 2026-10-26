using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.HR.Reports;

public static class HrTableExportProjection
{
	public static object? Value(string table, string field, JsonElement row, DateOnly? schoolToday = null)
	{
		object? Get(string path)
		{
			var value = row;
			foreach (var part in path.Split('.')) if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(part, out value)) return null;
			return value.ValueKind switch { JsonValueKind.String => value.GetString(), JsonValueKind.Number => value.GetDecimal(), JsonValueKind.True => true, JsonValueKind.False => false, JsonValueKind.Null => null, _ => value.ToString() };
		}
		string Text(string name) => Convert.ToString(Get(name)) ?? "";
		if (table == "hr-icard-templates" && field == "status") return Get("isActive") is true ? "Active" : "Inactive";
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
			"effective" => $"{Text("effectiveFrom")} to {Text("effectiveTo")}", "status" => Get("isActive") is true ? "Active" : "Inactive",
			"reason" => Get("assignmentReason"), _ => Get(field) };
		if (table == "hr-policy-versions" && field == "changeReason") return Get("changeReason") ?? Get("summary");
		if (table == "hr-performance-templates") return field switch {
			"sections" => row.TryGetProperty("sections", out var sections) ? string.Join("; ", sections.EnumerateArray().Select(x => x.GetProperty("name").GetString() + ": " + x.GetProperty("weight") + "%")) : null,
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
		if (table == "employee-onboarding-table") return field switch { "candidate" => Get("fullName"), "position" => Get("positionAppliedFor"), _ => Get(field) };
		if (table == "hr-attendance-register") return field switch
		{
			"exceptions" => $"Absent: {Text("absent")}; half day: {Text("halfDay")}; missing punch: {Text("missingPunch")}",
			"editability" => $"Editable: {Text("editable")}; locked: {Text("locked")}", _ => Get(field)
		};
		if (table == "hr-payroll-registration-readiness") return field switch
		{
			"applyPf" => Get("applyPf") is true ? Get("pfNumberPresent") is true ? "Number recorded" : "Number missing" : "Turned off",
			"applyEsic" => Get("applyEsic") is true ? Get("esicNumberPresent") is true ? "Number recorded" : "Number missing" : "Turned off", _ => Get(field)
		};
		if (table == "payroll-monthly-workbench-table") return field switch {
			"netSalary" => Get("netPay"),
			"status" => Get("isLocked") is true ? "LOCKED" : Get("reviewStatus"),
			"issueFilter" => PayrollIssues(row), _ => Get(field) };
		if (table is "hr-leave-requests" or "hr-self-leave-history") return field switch { "submittedBy" => Get("submittedByName"), "employeeName" => Get("name"), "employeeIdentifier" => Get("employeeIdentifier"), "organization" => Get("mediumStageName"), "reason" => Get("description"), _ => Get(field) };
		if (table == "hr-manual-attendance") return field switch {
			"employeeName" => Get("fullName"), "attendanceDate" => Get("dateIso"), "timeIn" => Get("inTime"), "timeOut" => Get("outTime"),
			"status" or "attendanceStatus" => Get("isHoliday") is true ? "Holiday" : Text("status") is "NotMarked" or "0" ? "Pending" : Get("status"),
			"errors" or "warnings" => row.TryGetProperty(field, out var values) ? string.Join("; ", values.EnumerateArray().Select(x => x.GetString())) : null, _ => Get(field) };
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
			"effective" => $"{Text("effectiveFrom")} to {Text("effectiveTo")}",
			"status" => Get("status"),
			"weeklyRule" => Text("weeklyOffPattern").Replace('_', ' '),
			"holidayCount" => Get("holidaySummary.includedCount"),
			"updatedAt" => Get("lastModificationTime") ?? Get("creationTime"),
			"version" => Get("versionNumber") ?? Get("version"),
			"calendar" => Get("workCalendarPolicyName"),
			"integration" => "Per staff attendance",
			"modeOrThresholds" => table == "hr-policy-payroll" ? $"{Text("dayCalculationType")} / {Text("saturdayWorkPolicy")}" : Text("captureMode") == "manual_status" ? "Manual status" : $"{(Text("captureMode") == "manual_with_time" ? "Manual with time" : "Integrated")} / {Text("fullDayThresholdPercentage")}%",
			_ => Get(field)
		};
		if (table == "hr-attendance-import-rows") return field is "errors" or "warnings" && row.TryGetProperty(field, out var messages) ? string.Join("; ", messages.EnumerateArray().Select(x => x.GetString())) : field == "attendanceStatus" ? Get("status") ?? "Pending" : Get(field);
		return Get(field);
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
