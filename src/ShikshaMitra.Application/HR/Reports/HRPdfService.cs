using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShikshaMitra.Common.PDF;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Reports;

public class HRPdfService(IPdfService pdfService)
{
	public byte[] GenerateEmployeeContactReport(IEnumerable<EmployeeContactDto> employees)
	{
		return pdfService.GeneratePdf(document =>
		{
			document.Page(page =>
			{
				page.Size(PageSizes.A4);
				page.Margin(2, Unit.Centimetre);
				page.Header().Text("Employee Report").FontSize(20).Bold().AlignCenter();
				page.Content().Table(table =>
				{
					table.ColumnsDefinition(columns =>
					{
						columns.RelativeColumn();
						columns.RelativeColumn();
						columns.RelativeColumn();
					});

					table.Header(header =>
					{
						header.Cell().Text("Code").Bold();
						header.Cell().Text("Name").Bold();
						header.Cell().Text("Date of Birth").Bold();
					});

					foreach (var employee in employees)
					{
						table.Cell().Text(employee.Code);
						table.Cell().Text(employee.EmployeeName);
						table.Cell().Text(employee.DateOfBirth?.ToString("dd-MM-yyyy"));
					}
				});
			});
		});
	}
}