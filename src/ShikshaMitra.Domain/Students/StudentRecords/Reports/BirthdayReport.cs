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
	internal class BirthdayReport : IDocument
	{
		private readonly IReadOnlyList<Student> _students;
		public BirthdayReport(IReadOnlyList<Student> students)
		{
			_students = students;
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
						c.Item().Text("Hedar Part");
					});
				});
			});
		}

		void ComposeContent(IContainer container)
		{
			container.PaddingVertical(20).Column(column =>
			{
				// Report Title
				// add line for whole page width
				column.Item().PaddingTop(10).BorderBottom(1).BorderColor(Colors.Black);

				column.Item().PaddingTop(10).Text("Birthday Report")
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
					});

					// Academic Performance Table Header
					table.Header(header =>
					{
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Birth Date").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("GR No.").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Class/Div").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Name").Bold();
					});

					// Sample data rows
					foreach (var item in _students)
					{
                        table.Cell().Border(1).Padding(5).Text(item.DateOfBirth?.ToString("dd/MM/yyyy"));
						table.Cell().Border(1).Padding(5).Text(item.GrNoRecord);
						table.Cell().Border(1).Padding(5).Text(item.Standard.Name + "/"+ item.Division.Name);
						table.Cell().Border(1).Padding(5).Text(item.LastName + " " + item.FirstName + " " + item.MiddleName);
					}

					

					//table.Cell().Border(1).Padding(5).Text("01/01/2025");
					//table.Cell().Border(1).Padding(5).Text("1235");
					//table.Cell().Border(1).Padding(5).Text("10/A");
					//table.Cell().Border(1).Padding(5).Text("Dharmik");

					//table.Cell().Border(1).Padding(5).Text("01/01/2025");
					//table.Cell().Border(1).Padding(5).Text("1236");
					//table.Cell().Border(1).Padding(5).Text("10/A");
					//table.Cell().Border(1).Padding(5).Text("Jeet");

					//table.Cell().Border(1).Padding(5).Text("01/01/2025");
					//table.Cell().Border(1).Padding(5).Text("1237");
					//table.Cell().Border(1).Padding(5).Text("10/A");
					//table.Cell().Border(1).Padding(5).Text("Jignesh");

					//table.Cell().Border(1).Padding(5).Text("01/01/2025");
					//table.Cell().Border(1).Padding(5).Text("1238");
					//table.Cell().Border(1).Padding(5).Text("10/A");
					//table.Cell().Border(1).Padding(5).Text("Parth");
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
