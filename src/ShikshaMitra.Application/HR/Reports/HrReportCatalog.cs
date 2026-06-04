using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.Reports;

internal static class HrReportCatalog
{
	public static List<HrReportOperatorDto> BuildOperators() =>
	[
		new() { Id = HrReportFilterOperator.Equals, Label = "Equals", Types = ["string", "number", "date", "boolean"] },
		new() { Id = HrReportFilterOperator.Contains, Label = "Contains", Types = ["string"] },
		new() { Id = HrReportFilterOperator.In, Label = "Is one of", Types = ["string", "number", "boolean"] },
		new() { Id = HrReportFilterOperator.GreaterThanOrEqual, Label = "On or after / >=", Types = ["date", "number"] },
		new() { Id = HrReportFilterOperator.LessThanOrEqual, Label = "On or before / <=", Types = ["date", "number"] }
	];

	public static List<HrReportOptionDto> BuildFormats() =>
	[
		new() { Id = HrReportFormat.Excel, Label = "Excel" },
		new() { Id = HrReportFormat.Pdf, Label = "PDF" }
	];

	public static HrReportSourceDto GetSource(string source)
	{
		return BuildSources().FirstOrDefault(x => x.Id == source.ToLowerInvariant())
			?? BuildSources().First(x => x.Id == HrReportDataSources.Employee);
	}

	public static List<HrReportColumnDto> ResolveColumns(string source, HrReportRequestDto input, List<Dictionary<string, string?>> rows)
	{
		var metadata = GetSource(source);
		var requested = input.Groups?.Count > 0
			? input.Groups.Concat(input.Summaries?.Select(x => $"{x.Function}_{x.Field}") ?? []).Prepend("rowCount").Distinct().ToList()
			: input.Columns is { Count: > 0 } ? input.Columns : metadata.DefaultColumns;

		var fields = metadata.Fields.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
		return requested
			.Where(id => rows.Count == 0 || rows[0].ContainsKey(id) || fields.ContainsKey(id) || id == "rowCount" || id.Contains('_'))
			.Select(id => new HrReportColumnDto
			{
				Id = id,
				Label = fields.TryGetValue(id, out var field)
					? field.Label
					: id == "rowCount" ? "Rows" : Humanize(id)
			})
			.ToList();
	}

	public static List<HrReportSourceDto> BuildSources()
	{
		return
		[
			Source(HrReportDataSources.Employee, "Employee", "Employee Reports", "Employee profile, contact, status, department, and reporting-manager data.",
				[
					Field("employeeCode", "Employee Code", groupable: true),
					Field("employeeName", "Employee Name"),
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
					Field("employeeCode", "Employee Code", groupable: true),
					Field("employeeName", "Employee Name"),
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
			Source(HrReportDataSources.Attendance, "Attendance", "Attendance Reports", "Daily attendance register and employee-wise status data.",
				[
					Field("attendanceDate", "Date", "date", groupable: true),
					Field("employeeCode", "Employee Code", groupable: true),
					Field("employeeName", "Employee Name"),
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
					Field("employeeCode", "Employee Code", groupable: true),
					Field("employeeName", "Employee Name"),
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
					Field("employeeCode", "Employee Code", groupable: true),
					Field("employeeName", "Employee Name"),
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

	private static HrReportFieldDto Field(string id, string label, string type = "string", bool groupable = false, bool summarizable = false)
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
			Operators = type switch
			{
				"number" or "date" => [HrReportFilterOperator.Equals, HrReportFilterOperator.GreaterThanOrEqual, HrReportFilterOperator.LessThanOrEqual],
				"boolean" => [HrReportFilterOperator.Equals],
				_ => [HrReportFilterOperator.Equals, HrReportFilterOperator.Contains, HrReportFilterOperator.In]
			}
		};
	}

	private static string Humanize(string id) => string.Concat(id.Select((ch, index) => index > 0 && char.IsUpper(ch) ? $" {ch}" : ch.ToString()));
}
