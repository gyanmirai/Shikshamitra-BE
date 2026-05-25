using ShikshaMitra.Common;
using ShikshaMitra.Common.Excel;
using ShikshaMitra.Common.Excel.Elements;
using ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Reports;

public class HRExcelService(IExcelService excelService) : ITransientDependency
{
	public byte[] GenerateEmployeeContactExcel(IEnumerable<EmployeeContactDto> employees)
	{
		var sheet = new ExcelSheet
		{
			SheetName = "Employee Contacts",
			Rows =
			[
				new ExcelRow
				{
					Cells =
					[
						new ExcelCell { Value = "Employee Name" },
						new ExcelCell { Value = "Email" },
						new ExcelCell { Value = "Mobile No" },
						new ExcelCell { Value = "Date of Birth" },
						new ExcelCell { Value = "Code" }
					]
				}
			]
		};

		sheet.Rows.AddRange(employees.Select(employee => new ExcelRow
		{
			Cells =
			[
				new ExcelCell { Value = employee.EmployeeName ?? "N/A" },
				new ExcelCell { Value = employee.Email ?? "N/A" },
				new ExcelCell { Value = employee.MobileNo ?? "N/A" },
				new ExcelCell { Value = employee.DateOfBirth?.ToString("dd-MM-yyyy") ?? "N/A" },
				new ExcelCell { Value = employee.EmployeeCode ?? "N/A" }
			]
		}));

		var document = new ExcelDocument { Sheets = [sheet], OutputFileName = "EmployeeContacts.xlsx" };

		return excelService.GenerateExcelAsBytes(document);
	}

	public byte[] GenerateLeaveManagementExcel(IReadOnlyList<LeaveManagementDto> rows)
	{
		var sheet = new ExcelSheet
		{
			SheetName = "Leave requests",
			Rows =
			[
				new ExcelRow
				{
					Cells =
					[
						new ExcelCell { Value = "Request #" },
						new ExcelCell { Value = "Name" },
						new ExcelCell { Value = "Employee code" },
						new ExcelCell { Value = "Leave type" },
						new ExcelCell { Value = "Start date" },
						new ExcelCell { Value = "End date" },
						new ExcelCell { Value = "Days" },
						new ExcelCell { Value = "Organization" },
						new ExcelCell { Value = "Description" },
						new ExcelCell { Value = "Status" }
					]
				}
			]
		};

		sheet.Rows.AddRange(rows.Select(r => new ExcelRow
		{
			Cells =
			[
				new ExcelCell { Value = r.PublicId.ToString() },
				new ExcelCell { Value = r.Name },
				new ExcelCell { Value = r.EmployeeIdentifier },
				new ExcelCell { Value = r.LeaveType },
				new ExcelCell { Value = r.StartDate.ToString("dd-MM-yyyy") },
				new ExcelCell { Value = r.EndDate.ToString("dd-MM-yyyy") },
				new ExcelCell { Value = r.TotalDays.ToString() },
				new ExcelCell { Value = r.MediumStageName },
				new ExcelCell { Value = r.Description },
				new ExcelCell { Value = r.Status }
			]
		}));

		var document = new ExcelDocument { Sheets = [sheet], OutputFileName = "leave-requests.xlsx" };

		return excelService.GenerateExcelAsBytes(document);
	}

	public byte[] GenerateReportExcel(HrReportPreviewDto report)
	{
		var sheet = new ExcelSheet
		{
			SheetName = SanitizeSheetName(report.ReportType),
			Rows =
			[
				new ExcelRow
				{
					Cells = report.Columns
						.Select(column => new ExcelCell { Value = column.Label })
						.ToList()
				}
			]
		};

		sheet.Rows.AddRange(report.Rows.Select(row => new ExcelRow
		{
			Cells = report.Columns
				.Select(column => new ExcelCell
				{
					Value = row.TryGetValue(column.Id, out var value) ? value ?? string.Empty : string.Empty
				})
				.ToList()
		}));

		return excelService.GenerateExcelAsBytes(new ExcelDocument
		{
			Sheets = [sheet],
			OutputFileName = $"{report.ReportType}.xlsx"
		});
	}

	private static string SanitizeSheetName(string name)
	{
		var invalidChars = new[] { ':', '\\', '/', '?', '*', '[', ']' };
		var sanitized = invalidChars.Aggregate(name, (current, ch) => current.Replace(ch, '-'));
		return string.IsNullOrWhiteSpace(sanitized)
			? "Report"
			: sanitized.Length <= 31 ? sanitized : sanitized[..31];
	}
}
