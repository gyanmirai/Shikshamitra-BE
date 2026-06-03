using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common.Excel;
using ShikshaMitra.Common.Excel.Elements;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Import;

[RemoteService(false)]
public class AttendanceImportTemplateService(IExcelService excelService)
	: ApplicationService, ITransientDependency
{
	public Task<AttendanceImportTemplateDto> GetTemplateAsync()
	{
		var document = new ExcelDocument
		{
			OutputFileName = "employee-attendance-import-template.xlsx",
			Sheets =
			[
				new ExcelSheet
				{
					SheetName = "AttendanceImport",
					Rows =
					[
						CreateExcelRow(AttendanceImportFileParser.AttendanceDateHeader, AttendanceImportFileParser.EmployeeCodeHeader, AttendanceImportFileParser.TimeInHeader, AttendanceImportFileParser.TimeOutHeader, AttendanceImportFileParser.StatusHeader, AttendanceImportFileParser.RemarksHeader, AttendanceImportFileParser.EmployeeNameHeader, AttendanceImportFileParser.ContactNoHeader),
						CreateExcelRow("11-04-2026", "EMP0001", "09:00 AM", "05:30 PM", string.Empty, "Regular shift", "John Doe", "9876543210"),
						CreateExcelRow("11-04-2026", "EMP0002", string.Empty, string.Empty, "FullDayLeave", "Approved leave", "Jane Doe", "9876543211"),
						CreateExcelRow("11-04-2026", "EMP0003", string.Empty, string.Empty, "HalfDayLeave", "Training half day", "Alex Doe", "9876543212")
					]
				}
			]
		};

		return Task.FromResult(new AttendanceImportTemplateDto
		{
			FileName = document.OutputFileName ?? "employee-attendance-import-template.xlsx",
			Content = excelService.GenerateExcelAsBytes(document)
		});
	}

	private static ExcelRow CreateExcelRow(params object?[] values)
	{
		return new ExcelRow
		{
			Cells = values.Select(x => new ExcelCell { Value = x ?? string.Empty }).ToList()
		};
	}
}
