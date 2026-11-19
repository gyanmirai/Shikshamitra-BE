using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.Reports;

public static class HrReportingCatalogue
{
	public static List<HrReportDatasetDto> Datasets()
	{
		var result = HrReportSourceDefinitions.Build().Where(x => x.Id != "payroll").Select(x => new HrReportDatasetDto
		{
			Id = x.Id,
			Label = x.Label,
			Category = x.Category,
			Fields = x.Fields,
			RowMeaning = x.Id switch
			{
				"employee" => "One staff member with current profile and lifecycle dates.",
				"attendance" => "One eligible staff member on one date, including dates with no attendance record.",
				"leave" => "One leave request overlapping the selected period.",
				"performance" => "One staff review assignment in a review cycle.",
				"policies" => "One policy assignment or one scoped HR audit event.",
				_ => "One staff member in one payroll month, using saved payroll evidence."
			}
		}).ToList();
		foreach (var dataset in result)
		{
			foreach (var f in dataset.Fields)
			{
				f.Filterable = true; f.Sortable = true;
				if (f.Id is "timeIn" or "timeOut" or "finalizedAt" or "occurredAt") f.Type = "datetime";
				if (f.Id is "netPay" or "totalEarnings" or "totalDeduction" or "grossSalary" or "basicSalary" or "salary" or "pf" or "pt" or "tds" or "esic") f.Type = "money";
				f.Operators = Field(f.Id, f.Label, f.Type).Operators;
				f.Summarizable = f.Type is "number" or "money";
				f.Groupable = f.Type is "string" or "date" or "boolean";
				f.Chartable = f.Groupable || f.Summarizable;
			}
			if (dataset.Id.StartsWith("payroll_", StringComparison.Ordinal) && !PayrollIssuedReportBoundary.IsSalaryDocument(dataset.Id))
				dataset.Parameters.Add("includeDrafts");
			if (dataset.Id == "performance") dataset.Parameters.Add("cycleId");
			if (dataset.Id is "attendance" or "employee") dataset.Parameters.AddRange(["academicYearId", "educationMediumStageId", "employeeGroup"]);
		}
		result.Single(x => x.Id == "attendance").Fields.AddRange([
			Field("employeeId", "Staff identity", "string"), Field("classification", "Day classification", "string"),
			Field("exception", "Exception", "string"), Field("punchIssue", "Punch issue", "string"), Field("periodState", "Attendance period", "string"), Field("holiday", "Holiday", "string")]);
		result.Single(x => x.Id == "attendance").Layouts.Add("attendanceMatrix");
		result.Single(x => x.Id == "payroll_register").Layouts.Add("salaryComponents");
		result.Single(x => x.Id == "employee").Fields.Add(Field("statusEffectiveDate", "Status effective date", "date"));
		result.Single(x => x.Id == "employee").Fields.AddRange([Field("leavingDate", "Leaving date", "date"), Field("lifecycleDate", "Joining or leaving date", "date")]);
		result.Single(x => x.Id == "performance").Parameters = ["cycleId", "employeeIds", "departments", "educationMediumStageId", "employeeGroup"];
		result.Single(x => x.Id == "leave").Fields.Add(Field("department", "Department", "string"));
		result.Single(x => x.Id == "leave").Parameters.Add("academicYearId");
		foreach (var payroll in result.Where(x => x.Id.StartsWith("payroll_", StringComparison.Ordinal))) payroll.Fields.AddRange([Field("department", "Current department", "string"), Field("payrollDepartment", "Department at issue", "string")]);
		result.Add(new HrReportDatasetDto
		{
			Id = "leave_balances",
			Label = "Current leave balances",
			Category = "Leave",
			RowMeaning = "One current staff leave-policy rule, calculated at generation time.",
			Parameters = ["academicYearId", "employeeIds", "departments"],
			Fields = [
				Field("employeeCode", "Staff code", "string"), Field("employeeName", "Staff name", "string"),
				Field("policy", "Leave policy", "string"), Field("leaveType", "Paid type", "string"),
				Field("accrued", "Accrued days", "number"), Field("carryForward", "Carry forward", "number"),
				Field("used", "Used days", "number"), Field("pending", "Pending days", "number"),
				Field("remaining", "Remaining days", "number"), Field("unlimited", "Unlimited", "boolean")]
		});
		foreach (var dataset in result)
			dataset.RequiredParameters = dataset.Id switch
			{
				"attendance" or "leave" or "payroll_register" => ["period"],
				"payroll_statement" => ["period", "employeeIds"],
				"leave_balances" => ["academicYearId"],
				_ => []
			};
		void Options(string source, string field, IEnumerable<string> values)
		{
			var target = result.Single(x => x.Id == source).Fields.Single(x => x.Id == field);
			target.Options = values.Select(x => new HrReportOptionDto { Id = x, Label = System.Text.RegularExpressions.Regex.Replace(x, "([a-z])([A-Z])", "$1 $2") }).ToList();
			target.Operators = ["equals", "notEquals", "in", "notIn", "empty", "notEmpty"];
		}
		Options("employee", "status", Enum.GetNames<ShikshaMitra.Common.EmployeeStatusType>());
		Options("attendance", "status", Enum.GetNames<ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister.AttendanceRegisterStatus>());
		Options("attendance", "exception", ["Not recorded", "Missing punch"]);
		Options("leave", "status", Enum.GetNames<ShikshaMitra.HR.AttendanceAndLeaves.EmployeeLeaveStatusType>());
		Options("performance", "status", Enum.GetNames<ShikshaMitra.HR.ProfessionalManagement.ProfessionalReviewStatus>());
		Options("policies", "recordType", ["Policy Assignment", "Audit"]);
		foreach (var source in new[] { "payroll_register", "payroll_statement" }) Options(source, "status", ["Issued", "Draft", "Unavailable"]);
		return result;
	}

	public static HrReportFieldDto Field(string id, string label, string type) => new()
	{
		Id = id,
		Label = label,
		Type = type,
		Filterable = true,
		Sortable = true,
		Groupable = type is "string" or "date" or "boolean",
		Summarizable = type is "number" or "money",
		Chartable = true,
		Operators = type is "number" or "money" or "date" or "datetime"
			? ["equals", "notEquals", "gt", "lt", "gte", "lte", "in", "notIn", "between", "empty", "notEmpty"]
			: type == "boolean" ? ["equals", "notEquals", "in", "notIn", "empty", "notEmpty"] : ["equals", "notEquals", "contains", "in", "notIn", "empty", "notEmpty"]
	};

	public static List<HrReportLibraryTemplateDto> BuiltIns()
	{
		var templates = new List<HrReportLibraryTemplateDto>();
		void Add(string key, string name, string dataset, string description, string[] columns, string layout = "detail", string? filter = null, string? value = null)
		{
			var metadata = Datasets().Single(x => x.Id == dataset);
			var definition = new HrReportDesignDto
			{
				DataSource = dataset,
				Layout = layout,
				Columns = columns.Select(x => new HrReportDisplayColumnDto { Field = x }).ToList(),
				Parameters = metadata.Parameters.ToList()
			};
			if (dataset == "employee") definition.Parameters.Remove("period");
			if (filter != null) definition.Filters.Conditions.Add(new() { Field = filter, Operator = "equals", Value = value });
			templates.Add(new()
			{
				Id = "builtin:" + key,
				Name = name,
				Description = description,
				Category = metadata.Category,
				Visibility = "builtin",
				IsBuiltIn = true,
				Status = "Published",
				Revision = 1,
				PublishedRevision = 1,
				Definition = definition
			});
		}
		Add("staff-directory", "Staff directory", "employee", "Current staff contact and department directory.", ["employeeCode", "employeeName", "department", "designation", "mobileNo", "email", "status"]);
		Add("department-strength", "Department strength", "employee", "Distinct staff count by department.", ["department"], "summary");
		templates[^1].Definition.Groups = ["department"]; templates[^1].Definition.Summaries = [new() { Field = "employeeCode", Function = "distinct" }];
		Add("staff-lifecycle", "Joining/leaving register", "employee", "Staff who joined or left within the selected period, using current lifecycle evidence.", ["employeeCode", "employeeName", "dateOfJoining", "leavingDate", "status", "statusEffectiveDate"]);
		templates[^1].Definition.Parameters.Add("period"); templates[^1].Definition.DateField = "lifecycleDate";
		Add("in-out", "Staff in/out register", "attendance", "Daily in/out times for every eligible selected staff member.", ["attendanceDate", "employeeCode", "employeeName", "department", "timeIn", "timeOut", "hoursWorked", "classification", "exception"]);
		Add("absences", "Marked absences", "attendance", "Recorded absences, keeping approved leave distinct.", ["attendanceDate", "employeeCode", "employeeName", "department", "status", "classification"], filter: "classification", value: "Absent");
		Add("not-recorded", "Attendance not recorded", "attendance", "Past expected working days with no recorded attendance or approved leave.", ["attendanceDate", "employeeCode", "employeeName", "department", "exception"], filter: "exception", value: "Not recorded");
		Add("missing-punches", "Missing punches", "attendance", "Incomplete integrated punches on closed attendance days.", ["attendanceDate", "employeeCode", "employeeName", "timeIn", "timeOut", "punchIssue"], filter: "exception", value: "Missing punch");
		Add("monthly-attendance", "Monthly attendance register", "attendance", "One row per staff member with a column for each day and status totals.", ["employeeCode", "employeeName", "department"], "attendanceMatrix");
		Add("leave-requests", "Leave requests", "leave", "Requests overlapping the selected period and their approved units.", ["requestNo", "employeeCode", "employeeName", "leaveType", "startDate", "endDate", "status", "approvedUnits"]);
		Add("pending-leave", "Pending leave approvals", "leave", "Requests awaiting an approval decision.", ["requestNo", "employeeCode", "employeeName", "startDate", "endDate", "status"], filter: "status", value: "Pending");
		Add("leave-usage", "Leave usage", "leave", "Approved leave units within the selected period by staff.", ["employeeCode", "employeeName"], "summary", "status", "Approved");
		templates[^1].Definition.Groups = ["employeeCode", "employeeName"]; templates[^1].Definition.Summaries = [new() { Field = "approvedUnits", Function = "sum" }];
		Add("leave-balances", "Current leave balances", "leave_balances", "Current accrued, used, pending and remaining leave for the chosen academic year.", ["employeeCode", "employeeName", "policy", "leaveType", "accrued", "carryForward", "used", "pending", "remaining", "unlimited"]);
		Add("salary-register", "School salary register", "payroll_register", "Saved finalized salary evidence for one payroll month.", ["employeeCode", "employeeName", "grossSalary", "pf", "pt", "tds", "esic", "totalEarnings", "totalDeduction", "netPay", "status"]);
		Add("salary-statement", "Staff salary statement", "payroll_statement", "Saved payroll across a chosen range of months for one staff member.", ["month", "employeeCode", "employeeName", "totalEarnings", "totalDeduction", "netPay", "status"]);
		Add("salary-components", "Earnings/deductions breakdown", "payroll_register", "One staff member per payroll month, with a column for every saved earning and deduction component.", ["employeeCode", "employeeName", "totalEarnings", "totalDeduction", "netPay", "status"], "salaryComponents");
		Add("department-payroll", "Department payroll summary", "payroll_register", "Saved earnings, deductions and net pay grouped by current staff department.", ["department"], "summary");
		templates[^1].Definition.Groups = ["department"]; templates[^1].Definition.Summaries = [new() { Field = "totalEarnings", Function = "sum" }, new() { Field = "totalDeduction", Function = "sum" }, new() { Field = "netPay", Function = "sum" }];
		Add("review-completion", "Review completion", "performance", "Review assignments and completion states by cycle.", ["cycle", "employeeCode", "employeeName", "department", "status"]);
		Add("review-outcomes", "Finalized outcomes", "performance", "Finalized review scores and ratings only.", ["cycle", "employeeCode", "employeeName", "finalScore", "finalRating", "finalizedAt"]);
		templates[^1].Definition.Filters.Conditions.Add(new() { Field = "finalizedAt", Operator = "notEmpty" });
		Add("policy-assignments", "Policy assignments", "policies", "Current policy assignment records within your scope.", ["entity", "target", "effectiveFrom", "effectiveTo", "isActive", "summary"], filter: "recordType", value: "Policy Assignment");
		templates[^1].Definition.Parameters.Remove("period");
		Add("hr-audit", "HR audit activity", "policies", "Scoped HR audit events in the selected period.", ["entity", "action", "target", "occurredAt", "summary"], filter: "recordType", value: "Audit");
		return templates;
	}
}
