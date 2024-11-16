using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShikshaMitra.Common.PDF;
using ShikshaMitra.Common.PDF.Elements;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IElement = ShikshaMitra.Common.PDF.Elements.IElement;

namespace ShikshaMitra.HR.Reports;

public class HRPdfService(IPdfService pdfService)
{
	public async Task<byte[]> GenerateEmployeeContactReport(IEnumerable<EmployeeContactDto> employees)
	{
		// Construct the PDF document
		var pdfDocument = new PDFDocument
		{
			Page = new PageSpecs
			{
				BackgroundColor = "#ffffff",
				Margin = 20,
				MarginUnit = Unit.Point
			},
			Header = new TextElement
			{
				Text = "Employee Contact Report",
				FontSize = 24,
				FontWeight = FontWeight.Bold,
				Alignment = TextAlignment.Center,
				Padding = 10,
				PaddingUnit = Unit.Point
			},
			Content = new ColumnElement
			{
				Elements = new List<IElement>
					{
                        // Add column headers
                        new RowElement
						{
							Elements = new List<IElement>
							{
								new TextElement
								{
									Text = "Employee Name",
									FontWeight = FontWeight.Bold,
									Padding = 5,
									PaddingUnit = Unit.Point
								},
								new TextElement
								{
									Text = "Email",
									FontWeight = FontWeight.Bold,
									Padding = 5,
									PaddingUnit = Unit.Point
								},
								new TextElement
								{
									Text = "Mobile No",
									FontWeight = FontWeight.Bold,
									Padding = 5,
									PaddingUnit = Unit.Point
								},
								new TextElement
								{
									Text = "Date of Birth",
									FontWeight = FontWeight.Bold,
									Padding = 5,
									PaddingUnit = Unit.Point
								},
								new TextElement
								{
									Text = "Code",
									FontWeight = FontWeight.Bold,
									Padding = 5,
									PaddingUnit = Unit.Point
								}
							},
							Spacing = 10
						}
					}.Concat(employees.Select(employee => (IElement) new RowElement
					{
						Elements = new List<IElement>
						{
							new TextElement
							{
								Text = employee.EmployeeName ?? "N/A",
								FontWeight = FontWeight.Bold,
								Padding = 5,
								PaddingUnit = Unit.Point
							},
							new TextElement
							{
								Text = employee.Email ?? "N/A",
								Padding = 5,
								PaddingUnit = Unit.Point
							},
							new TextElement
							{
								Text = employee.MobileNo ?? "N/A",
								Padding = 5,
								PaddingUnit = Unit.Point
							},
							new TextElement
							{
								Text = employee.DateOfBirth?.ToString("dd-MM-yyyy") ?? "N/A",
								Padding = 5,
								PaddingUnit = Unit.Point
							},
							new TextElement
							{
								Text = employee.EmployeeCode ?? "N/A",
								Padding = 5,
								PaddingUnit = Unit.Point
							}
						},
						Spacing = 10
					})).ToList(),
				Spacing = 20
			}
		};
		return await pdfService.GeneratePdfAsBytes(pdfDocument);
	}

}