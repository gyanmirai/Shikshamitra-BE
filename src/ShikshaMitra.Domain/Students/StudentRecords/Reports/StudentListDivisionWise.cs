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
	public class StudetListDivisionnWise : IDocument
	{
		private readonly IReadOnlyList<Student> _student;
		public StudetListDivisionnWise(IReadOnlyList<Student> student)
		{
			_student = student;
		}
		public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

		public void Compose(IDocumentContainer container)
		{
			container.Page(page =>
			{
				page.Size(PageSizes.A4);
				page.Margin(30);
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
						c.Item().Text("Header part");
					});
				});
			});
		}  // ✅ Closing brace added for ComposeHeader

		void ComposeContent(IContainer container)
		{
			container.PaddingVertical(20).Column(column =>
			{

				// Report Title
				column.Item().PaddingTop(10).BorderBottom(1).BorderColor(Colors.Black);
				column.Item().PaddingTop(10).AlignCenter().Text("Student List - Division Wise")
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
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("SR. No").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("GR No.").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Roll No.").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Name").Bold();
					});

					// Sample data rows

					foreach (var (item,index) in _student.Select((s,i)=>(s,i+1)))
					{
					table.Cell().Border(1).Padding(5).Text($"{index}.");
					table.Cell().Border(1).Padding(5).Text(item.GrNoRecord);
					table.Cell().Border(1).Padding(5).Text(item.RollNo);
					table.Cell().Border(1).Padding(5).Text($"{item.LastName} {item.FirstName} ${item.MiddleName}");

						
					}
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
