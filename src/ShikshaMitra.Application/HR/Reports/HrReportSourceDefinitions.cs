using System.Collections.Generic;

namespace ShikshaMitra.HR.Reports;

internal static class HrReportSourceDefinitions
{
	public static List<HrReportSourceDto> Build()
	{
		return
		[
			PayrollDocumentSource(HrReportDataSources.PayrollStatement, "Employee salary statement"),
			PayrollDocumentSource(HrReportDataSources.PayrollRegister, "School payroll register"),
			Source(HrReportDataSources.Employee, "Staff", "Staff Reports", "Staff profile, contact, status, department, and reporting-manager data.",
				[
					Field("employeeCode", "Staff Code", groupable: true),
					Field("employeeName", "Staff Name"),
					Field("department", "Department", groupable: true),
					Field("designation", "Designation", groupable: true),
					Field("status", "Status", groupable: true),
					Field("gender", "Gender", groupable: true),
					Field("mobileNo", "Mobile"),
					Field("email", "Email"),
					Field("dateOfJoining", "Date of Joining", "date"),
					Field("dateOfBirth", "Date of Birth", "date")
				],
				["employeeCode", "employeeName", "department", "designation", "status", "mobileNo", "email"],
				["employeeCode"]),
			Source(HrReportDataSources.Payroll, "Payroll", "Payroll Reports", "Salary register, payslip, allowance, deduction, reconciliation, and settlement rows.",
				[
					Field("month", "Month", groupable: true),
					Field("employeeCode", "Staff Code", groupable: true),
					Field("employeeName", "Staff Name"),
					Field("department", "Department", groupable: true),
					Field("designation", "Designation", groupable: true),
					Field("basicSalary", "Basic Salary", "number", summarizable: true),
					Field("grossSalary", "Gross Salary", "number", summarizable: true),
					Field("totalDeduction", "Total Deduction", "number", summarizable: true),
					Field("netPay", "Net Pay", "number", summarizable: true),
					Field("payableDays", "Payable Days", "number", summarizable: true),
					Field("reviewStatus", "Review Status", groupable: true),
					Field("isLocked", "Locked", "boolean", groupable: true)
				],
				["month", "employeeCode", "employeeName", "department", "grossSalary", "totalDeduction", "netPay", "reviewStatus"],
				["employeeCode"]),
			Source(HrReportDataSources.Attendance, "Attendance", "Attendance Reports", "Daily attendance register and staff-wise status data.",
				[
					Field("attendanceDate", "Date", "date", groupable: true),
					Field("employeeCode", "Staff Code", groupable: true),
					Field("employeeName", "Staff Name"),
					Field("department", "Department", groupable: true),
					Field("status", "Status", groupable: true),
					Field("timeIn", "Time In", "date"),
					Field("timeOut", "Time Out", "date"),
					Field("hoursWorked", "Hours Worked", "number", summarizable: true),
					Field("remarks", "Remarks")
				],
				["attendanceDate", "employeeCode", "employeeName", "department", "status", "timeIn", "timeOut", "hoursWorked"],
				["attendanceDate", "employeeCode"]),
			Source(HrReportDataSources.Leave, "Leave", "Leave Reports", "Leave requests, approvals, balances, and leave usage exports.",
				[
					Field("requestNo", "Request #", "number"),
					Field("employeeCode", "Staff Code", groupable: true),
					Field("employeeName", "Staff Name"),
					Field("leaveType", "Leave Type", groupable: true),
					Field("startDate", "Start Date", "date"),
					Field("endDate", "End Date", "date"),
					Field("status", "Status", groupable: true),
					Field("approvedUnits", "Approved Units", "number", summarizable: true),
					Field("description", "Description")
				],
				["requestNo", "employeeCode", "employeeName", "leaveType", "startDate", "endDate", "status", "approvedUnits"],
				["startDate"]),
			Source(HrReportDataSources.Policies, "Policies", "Policies And Audit Reports", "Policy assignments and HR audit activity.",
				[
					Field("recordType", "Record Type", groupable: true),
					Field("entity", "Entity"),
					Field("action", "Action", groupable: true),
					Field("target", "Target"),
					Field("effectiveFrom", "Effective From", "date"),
					Field("effectiveTo", "Effective To", "date"),
					Field("isActive", "Active", "boolean", groupable: true),
					Field("occurredAt", "Occurred At", "date"),
					Field("summary", "Summary")
				],
				["recordType", "entity", "action", "target", "effectiveFrom", "effectiveTo", "isActive", "summary"],
				["recordType", "entity"]),
			Source(HrReportDataSources.Performance, "Performance", "Performance Reports", "Review cycles, assignments, scores, outcomes, and calibration views.",
				[
					Field("cycle", "Cycle", groupable: true),
					Field("employeeCode", "Staff Code", groupable: true),
					Field("employeeName", "Staff Name"),
					Field("department", "Department", groupable: true),
					Field("status", "Status", groupable: true),
					Field("finalScore", "Final Score", "number", summarizable: true),
					Field("finalRating", "Final Rating", groupable: true),
					Field("finalizedAt", "Finalized At", "date")
				],
				["cycle", "employeeCode", "employeeName", "department", "status", "finalScore", "finalRating"],
				["cycle", "employeeCode"])
		];
	}

	private static HrReportSourceDto PayrollDocumentSource(string id, string label)
	{
		var fields = new List<HrReportFieldDto> { Field("month", "Month"), Field("employeeCode", "Staff code"), Field("employeeName", "Staff name"),
			Field("designation", "Designation"), Field("dateOfJoining", "Joining date", "date"), Field("bankAccount", "Bank account"), Field("pfNumber", "PF number"), Field("esicNumber", "ESIC number"), Field("status", "Document status") };
		foreach (var (code, name) in new[] { ("salary", "Monthly salary"), ("grossSalary", "Earned gross"), ("basicSalary", "Basic salary"),
			("pfSalary", "PF salary"), ("pf", "PF"), ("pt", "PT"), ("tds", "TDS"), ("esic", "ESIC"), ("otherDeductions", "Other deductions"),
			("totalEarnings", "Total earnings"), ("totalDeduction", "Total deductions"), ("netPay", "Net pay"), ("casualLeaveDays", "CL"), ("paidLeaveDays", "Paid leave"), ("unpaidLeaveDays", "LWP"), ("payableDays", "Payable days") })
			fields.Add(Field(code, name, "number"));
		foreach (var field in fields)
		{
			field.Filterable = false;
			field.Sortable = field.Id is "month" or "employeeCode" or "netPay" or "totalEarnings" or "totalDeduction";
		}
		return Source(id, "Payroll", label, "Saved payroll evidence. Missing months and unavailable historical fields remain visible.", fields,
			["month", "employeeCode", "employeeName", "grossSalary", "pf", "pt", "tds", "esic", "totalEarnings", "totalDeduction", "netPay", "casualLeaveDays", "unpaidLeaveDays", "payableDays", "status"], ["month", "employeeCode"]);
	}

	private static HrReportSourceDto Source(string id, string category, string label, string description, List<HrReportFieldDto> fields, List<string> defaultColumns, List<string> defaultSorts)
	{
		return new HrReportSourceDto
		{
			Id = id,
			Category = category,
			Label = label,
			Description = description,
			Fields = fields,
			DefaultColumns = defaultColumns,
			DefaultSorts = defaultSorts
		};
	}

	private static HrReportFieldDto Field(string id, string label, string type = "string", bool groupable = false, bool summarizable = false, bool? chartable = null)
	{
		return new HrReportFieldDto
		{
			Id = id,
			Label = label,
			Type = type,
			Filterable = true,
			Sortable = true,
			Groupable = groupable,
			Summarizable = summarizable,
			Chartable = chartable ?? (groupable || summarizable),
			Operators = type switch
			{
				"number" or "date" => [HrReportFilterOperator.Equals, HrReportFilterOperator.GreaterThanOrEqual, HrReportFilterOperator.LessThanOrEqual],
				"boolean" => [HrReportFilterOperator.Equals],
				_ => [HrReportFilterOperator.Equals, HrReportFilterOperator.Contains, HrReportFilterOperator.In]
			}
		};
	}
}
