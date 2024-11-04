using ShikshaMitra.Common;
using ShikshaMitra.Common.Elements;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.Reports;

public class HRExcelService(IExcelService excelService)
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
				new ExcelCell { Value = employee.Code ?? "N/A" }
			]
		}));

		var document = new ExcelDocument { Sheets = [sheet], OutputFileName = "EmployeeContacts.xlsx" };

		return excelService.GenerateExcelAsBytes(document);
	}
}