using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentRecords.Reports
{
	public class TranscriptionCertificate : IDocument
	{
		private readonly Student _student;
		public TranscriptionCertificate(Student student)
		{
			_student = student;
		}
		public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

		public void Compose(IDocumentContainer container)
		{
			container.Page(page =>
			{
				page.Size(PageSizes.A4);
				page.Margin(50);
				page.DefaultTextStyle(x => x.FontFamily("Arial"));

				page.Header().Element(ComposeHeader);
				page.Content().Element(ComposeContent);
				page.Footer().Element(ComposeFooter);
			});
		}

		void ComposeHeader(IContainer container)
		{
			container.Column(column =>
			{
				column.Item().Row(row =>
				{
					row.RelativeItem().AlignCenter().Column(c =>
					{
						c.Item().Text("ShikshaMitra").FontSize(20).Bold();
					});
				});
			});
		}

		void ComposeContent(IContainer container)
		{
			container.PaddingVertical(20).Column(column =>
			{
				column.Item().PaddingTop(10).BorderBottom(1).BorderColor(Colors.Black);
				column.Item().PaddingTop(10).AlignCenter().Text("Transcription Certificate")
					.FontSize(16)
					.Bold();
				column.Item().PaddingTop(10).BorderBottom(1).BorderColor(Colors.Black);
				// Student Details Section
				column.Item().PaddingTop(20).Element(e =>
				{
					e.Table(table =>
					{
						table.ColumnsDefinition(columns =>
						{
							columns.RelativeColumn();
						});

						AddDetailRow(table.Cell(), "Name Of Applicant", $"{_student.LastName} {_student.FirstName} {_student.MiddleName}");
						AddDetailRow(table.Cell(), "Date Of Birth", _student.DateOfBirth?.ToString("dd/MM/yyyy") ?? "");
						AddDetailRow(table.Cell(), "Permanent Address", _student.AddressLine1);
						AddDetailRow(table.Cell(), "Institute Where Studied", "DDVS - EM - PRIMARY\n2 Navagam");
						AddDetailRow(table.Cell(), "Diploma Awarded", "Common");
						AddDetailRow(table.Cell(), "Registration No", "2392");
						AddDetailRow(table.Cell(), "Board Confirming The Diploma", "");
						AddDetailRow(table.Cell(), "Month Of Entrance", "05/06/2023");
						AddDetailRow(table.Cell(), "Duration Of Course", "");
						AddDetailRow(table.Cell(), "Medium Of Instruction", _student.EducationMedium.Name);
						AddDetailRow(table.Cell(), "Grading Scheme", "");
					});
				});

				// Academic Performance Section
				column.Item().PaddingTop(20).Text($"Standard {_student.Standard.Name}")
					.FontSize(14)
					.Bold();

				column.Item().Element(e =>
				{
					e.Table(table =>
					{
						table.ColumnsDefinition(columns =>
						{
							columns.RelativeColumn();
							columns.RelativeColumn();
							columns.RelativeColumn();
							columns.RelativeColumn();
							columns.RelativeColumn();
						});

						// Table Header
						table.Header(header =>
						{
							header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Sub Code").Bold();
							header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Sub Name").Bold();
							header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Year").Bold();
							header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Hours Per Week").Bold();
							header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Marks").Bold();
						});

						// Sample Data Row
						AddStudentRow(table, "2024-2025", "2024-2025", "2024-2025", "20", "50");

						//AddStudentRow(table.Cell(), "", "", "2024-2025", "", "");
						//AddStudentRow(table.Cell(), "", "", "2024-2025", "", "");
						//AddStudentRow(table.Cell(), "", "", "2024-2025", "", "");
						//AddStudentRow(table.Cell(), "", "", "2024-2025", "", "");
						//AddStudentRow(table.Cell(), "", "", "2024-2025", "", "");
						//AddStudentRow(table.Cell(), "", "", "2024-2025", "", "");
					});
				});
			});
		}

		void ComposeFooter(IContainer container)
		{
			container.Row(row =>
			{
				row.RelativeItem().AlignLeft().Text(text =>
				{
					text.CurrentPageNumber();
					text.Span(" / ");
					text.TotalPages();
				});
				row.RelativeItem().AlignRight().Text($"Generated on {DateTime.Now:dd MMM yyyy}");
			});
		}

		private void AddDetailRow(IContainer container, string label, string value)
		{
			container.Row(row =>
			{
				row.RelativeItem().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text(label).Bold();
				row.RelativeItem().Border(1).Padding(5).Text(value);
			});
		}

		private void AddStudentRow(TableDescriptor table, string subCode, string subName, string year, string hoursPerWeek, string marks)
		{
			//container.Row(row =>
			//{
			//    table.Cell().Border(1).Padding(5).Text(className);


			//});

			table.Cell().Padding(5).Text(subCode);
			table.Cell().Padding(5).Text(subName);
			table.Cell().Padding(5).Text(year);
			table.Cell().Padding(5).Text(hoursPerWeek);
			table.Cell().Padding(5).Text(marks);
		}
	}
}
