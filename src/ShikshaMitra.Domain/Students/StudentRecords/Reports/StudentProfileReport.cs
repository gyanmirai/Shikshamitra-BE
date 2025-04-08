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
	public class StudentProfileReport : IDocument
	{

		private readonly Student _student;
		public StudentProfileReport(Student student)
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
				column.Item().PaddingTop(10).BorderBottom(1).BorderColor(Colors.Black);
				column.Item().PaddingTop(10).AlignCenter().Text("Student Profile Report")
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

					AddDetailRow(table, "Admission Number", "2024/001");
					AddDetailRow(table, "Student Name", $"{_student.LastName} {_student.FirstName} ${_student.MiddleName}");
					AddDetailRow(table, "Class & Section", $"{_student.Standard.Name} - {_student.Division.Name}");
					AddDetailRow(table, "Date of Birth", _student.DateOfBirth?.ToString("dd-MM-yyyy") ?? string.Empty);
					AddDetailRow(table, "Parent/Guardian", _student.FatherDetails.Name);
					AddDetailRow(table, "Contact Number", _student.ParentContactNumber);
					AddDetailRow(table, "Address", _student.AddressLine1);
					AddDetailRow(table, "Attendance", "-");
				});

				// Academic Performance Section
				column.Item().PaddingTop(20).Text("Academic Performance")
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
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Subject").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Max Marks").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Marks Obtained").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Grade").Bold();
					});

					// Sample data rows
					table.Cell().Border(1).Padding(5).Text("Mathematics");
					table.Cell().Border(1).Padding(5).Text("100");
					table.Cell().Border(1).Padding(5).Text("92");
					table.Cell().Border(1).Padding(5).Text("A+");

					table.Cell().Border(1).Padding(5).Text("Science");
					table.Cell().Border(1).Padding(5).Text("100");
					table.Cell().Border(1).Padding(5).Text("88");
					table.Cell().Border(1).Padding(5).Text("A");

					table.Cell().Border(1).Padding(5).Text("English");
					table.Cell().Border(1).Padding(5).Text("100");
					table.Cell().Border(1).Padding(5).Text("85");
					table.Cell().Border(1).Padding(5).Text("A");

					table.Cell().Border(1).Padding(5).Text("Hindi");
					table.Cell().Border(1).Padding(5).Text("100");
					table.Cell().Border(1).Padding(5).Text("90");
					table.Cell().Border(1).Padding(5).Text("A+");

					table.Cell().Border(1).Padding(5).Text("Social Studies");
					table.Cell().Border(1).Padding(5).Text("100");
					table.Cell().Border(1).Padding(5).Text("87");
					table.Cell().Border(1).Padding(5).Text("A");
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
			table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text(label).Bold();
			table.Cell().Border(1).Padding(5).Text(value);
		}
	}
}
