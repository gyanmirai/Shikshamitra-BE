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
	public class StudentRegister : IDocument
	{

		private readonly IReadOnlyList<Student> _student;

		public StudentRegister(IReadOnlyList<Student> student)
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
						c.Item().Text("Header Part");
					});
				});
			});
		}  // ✅ Closing brace added for ComposeHeader

		void ComposeContent(IContainer container)
		{
			container.PaddingVertical(20).Column(column =>
			{
				// Report Title
				// add line for whole page width
				column.Item().PaddingTop(10).BorderBottom(1).BorderColor(Colors.Black);

				column.Item().PaddingTop(10).Text("Student Register")
					.FontSize(16).Bold().AlignCenter();

				column.Item().PaddingTop(10).BorderBottom(1).BorderColor(Colors.Black);

				// Student Details Section
				column.Item().PaddingTop(20).Table(table =>
				{
					table.ColumnsDefinition(columns =>
					{
						columns.RelativeColumn();
						columns.RelativeColumn();
						columns.RelativeColumn();
						columns.RelativeColumn();
					});

					AddDetailRow(table, "Batch", "2024-2025");
					AddDetailRow(table, "Class", "02");
					AddDetailRow(table, "Division", "A");
					AddDetailRow(table, "Status", "Active");


				});

				// Academic Performance Section
				column.Item().PaddingTop(20).Text("")
					.FontSize(14)
					.Bold();

				column.Item().Table(table =>
				{
					table.ColumnsDefinition(columns =>
					{
						columns.RelativeColumn();
						columns.RelativeColumn();
						columns.RelativeColumn();
						columns.RelativeColumn();
						columns.RelativeColumn();
						columns.RelativeColumn();
						columns.RelativeColumn();
						columns.RelativeColumn();
					});

					// Academic Performance Table Header
					table.Header(header =>
					{
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("GR No.").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Surname").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Nationality").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Birth Place").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Birth Date").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Previous School").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Reason Of Leaving").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("LC No").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("SUID").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Name").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Mother Tongue").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Birth Taluka").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Birth Date in Word").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Admission Dated").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Date of Leaving").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Date").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Aadhar Card No.").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Middle Name").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Religion").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Birth District").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Admission Standard").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Leaving Standard").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Father Name").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Caste").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Birth State").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Academic Performance").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Mother Name").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("SubCaste").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Birth Country").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Behavior").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("").Bold();
					});

					// Sample data rows
					table.Cell().Border(1).Padding(5).Text("1234");
					table.Cell().Border(1).Padding(5).Text("John");
					table.Cell().Border(1).Padding(5).Text("Indian");
					table.Cell().Border(1).Padding(5).Text("Surat");
					table.Cell().Border(1).Padding(5).Text("02/10/2010");
					table.Cell().Border(1).Padding(5).Text("");
					table.Cell().Border(1).Padding(5).Text("");
					table.Cell().Border(1).Padding(5).Text("");
					table.Cell().Border(1).Padding(5).Text("");
					table.Cell().Border(1).Padding(5).Text("Doe");
					table.Cell().Border(1).Padding(5).Text("Gujarati");
					table.Cell().Border(1).Padding(5).Text("");
					table.Cell().Border(1).Padding(5).Text("Second October");
					table.Cell().Border(1).Padding(5).Text("05/06/2023");
					table.Cell().Border(1).Padding(5).Text("");
					table.Cell().Border(1).Padding(5).Text("");
					table.Cell().Border(1).Padding(5).Text("785420005949");
					table.Cell().Border(1).Padding(5).Text("MartinBhai");
					table.Cell().Border(1).Padding(5).Text("Hindu");
					table.Cell().Border(1).Padding(5).Text("");
					table.Cell().Border(1).Padding(5).Text("");
					table.Cell().Border(1).Padding(5).Text("1");
					table.Cell().Border(1).Padding(5).Text("");
					table.Cell().Border(1).Padding(5).Text("");
					table.Cell().Border(1).Padding(5).Text("");
					table.Cell().Border(1).Padding(5).Text("MartinBhai");
					table.Cell().Border(1).Padding(5).Text("Hindu Ahir");
					table.Cell().Border(1).Padding(5).Text("");
					table.Cell().Border(1).Padding(5).Text("");
					table.Cell().Border(1).Padding(5).Text("");
					table.Cell().Border(1).Padding(5).Text("");
					table.Cell().Border(1).Padding(5).Text("");
					table.Cell().Border(1).Padding(5).Text("");
					table.Cell().Border(1).Padding(5).Text("SelinaBen");
					table.Cell().Border(1).Padding(5).Text("Hindu Ahir");
					table.Cell().Border(1).Padding(5).Text("");
					table.Cell().Border(1).Padding(5).Text("");
					table.Cell().Border(1).Padding(5).Text("");
					table.Cell().Border(1).Padding(5).Text("");
					table.Cell().Border(1).Padding(5).Text("");

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

		private void AddDetailRow(TableDescriptor table, string label, string value)
		{
			table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(label).Bold();
			table.Cell().Padding(5).Text(value);
		}
	}
}
