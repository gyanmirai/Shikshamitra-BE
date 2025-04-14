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
	public class LeftStudentsList : IDocument
	{
		private readonly IReadOnlyList<Student> _student;
		public LeftStudentsList(IReadOnlyList<Student> student)
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

		private void ComposeFooter(IContainer container)
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

		private void ComposeContent(IContainer container)
		{
			container.PaddingVertical(20).Column(column =>
			{
				// Report Title
				column.Item().PaddingTop(10).BorderBottom(1).BorderColor(Colors.Black);
				column.Item().PaddingTop(10).AlignCenter().Text("Left Students List")
					.FontSize(16)
					.Bold();
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

					AddDetailRow(table, "Class", "10th");
					AddDetailRow(table, "Divsion", "A");
					AddDetailRow(table, "Status", "Left");
					AddDetailRow(table, "To Date", "01/01/2025");
					AddDetailRow(table, "End Date", "01/02/2025");
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
					});

					// Academic Performance Table Header
					table.Header(header =>
					{
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Roll No.").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("GR No.").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Name").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Admission Date").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Left Date").Bold();
					});

					// Sample data rows
					table.Cell().Border(1).Padding(5).Text("01");
					table.Cell().Border(1).Padding(5).Text("1234");
					table.Cell().Border(1).Padding(5).Text("John Doe");
					table.Cell().Border(1).Padding(5).Text("05/06/2024");
					table.Cell().Border(1).Padding(5).Text("01/05/2025");

					table.Cell().Border(1).Padding(5).Text("02");
					table.Cell().Border(1).Padding(5).Text("1235");
					table.Cell().Border(1).Padding(5).Text("Dharmik");
					table.Cell().Border(1).Padding(5).Text("05/06/2024");
					table.Cell().Border(1).Padding(5).Text("01/05/2025");

					table.Cell().Border(1).Padding(5).Text("03");
					table.Cell().Border(1).Padding(5).Text("1236");
					table.Cell().Border(1).Padding(5).Text("Jeet");
					table.Cell().Border(1).Padding(5).Text("05/06/2024");
					table.Cell().Border(1).Padding(5).Text("01/05/2025");

					table.Cell().Border(1).Padding(5).Text("04");
					table.Cell().Border(1).Padding(5).Text("1237");
					table.Cell().Border(1).Padding(5).Text("Jignesh");
					table.Cell().Border(1).Padding(5).Text("05/06/2024");
					table.Cell().Border(1).Padding(5).Text("01/05/2025");

					table.Cell().Border(1).Padding(5).Text("05");
					table.Cell().Border(1).Padding(5).Text("1238");
					table.Cell().Border(1).Padding(5).Text("Parth");
					table.Cell().Border(1).Padding(5).Text("05/06/2024");
					table.Cell().Border(1).Padding(5).Text("01/05/2025");
				});
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
		}

		private void AddDetailRow(TableDescriptor table, string label, string value)
		{
			table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(label).Bold();
			table.Cell().Padding(5).Text(value);
		}
	}
}
