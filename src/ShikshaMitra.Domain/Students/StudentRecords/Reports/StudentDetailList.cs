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
	public class StudentDetailList : IDocument
	{
		public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

		private readonly IReadOnlyList<Student> _student;

		public StudentDetailList(IReadOnlyList<Student> student)
		{
			_student = student;
		}
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

		//create method to get all female studends
		

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

				column.Item().PaddingTop(10).Text("Student Detail List")
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
					});

					// Academic Performance Table Header
					table.Header(header =>
					{
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Roll No.").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("GR No.").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Name").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Birth Date").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Mobile No.").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Gender").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Caste").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Category").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("").Bold();
					});

					// Sample data rows
                    foreach (var item in _student)
                    {
                        table.Cell().Border(1).Padding(5).Text(item.RollNo.ToString());
                        table.Cell().Border(1).Padding(5).Text(item.GrNoRecord.ToString());
                        table.Cell().Border(1).Padding(5).Text($"{item.LastName} {item.FirstName} {item.MiddleName}");
                        table.Cell().Border(1).Padding(5).Text(item.DateOfBirth?.ToString("dd-MM-yyyy") ?? string.Empty);
                        table.Cell().Border(1).Padding(5).Text(item.ParentContactNumber);
						table.Cell().Border(1).Padding(5).Text(item.GenderType.ToString());
						table.Cell().Border(1).Padding(5).Text(item.CastType);
						table.Cell().Border(1).Padding(5).Text(item.Religion);

						table.Cell().Border(1).Padding(5).Text("OBC");
						table.Cell().Border(1).Padding(5).Text("");
						table.Cell().Border(1).Padding(5).Text("");
						table.Cell().Border(1).Padding(5).Text("");
						table.Cell().Border(1).Padding(5).Text("");
					}
					//table.Cell().Border(1).Padding(5).Text("01-01-2000");
					//table.Cell().Border(1).Padding(5).Text("8185654228");
					//table.Cell().Border(1).Padding(5).Text("Male");
					//table.Cell().Border(1).Padding(5).Text("Ahir");
					

					//table.Cell().Border(1).Padding(5).Text("2");
					//table.Cell().Border(1).Padding(5).Text("1235");
					//table.Cell().Border(1).Padding(5).Text("Dharmik Jinjala");
					//table.Cell().Border(1).Padding(5).Text("01-01-2000");
					//table.Cell().Border(1).Padding(5).Text("8185654228");
					//table.Cell().Border(1).Padding(5).Text("Male");
					//table.Cell().Border(1).Padding(5).Text("Ahir");
					//table.Cell().Border(1).Padding(5).Text("OBC");
					//table.Cell().Border(1).Padding(5).Text("");
					//table.Cell().Border(1).Padding(5).Text("");
					//table.Cell().Border(1).Padding(5).Text("");
					//table.Cell().Border(1).Padding(5).Text("");


					//table.Cell().Border(1).Padding(5).Text("3");
					//table.Cell().Border(1).Padding(5).Text("1236");
					//table.Cell().Border(1).Padding(5).Text("Jeet Ram");
					//table.Cell().Border(1).Padding(5).Text("01-01-2000");
					//table.Cell().Border(1).Padding(5).Text("8185654228");
					//table.Cell().Border(1).Padding(5).Text("Male");
					//table.Cell().Border(1).Padding(5).Text("Ahir");
					//table.Cell().Border(1).Padding(5).Text("OBC");
					//table.Cell().Border(1).Padding(5).Text("");
					//table.Cell().Border(1).Padding(5).Text("");
					//table.Cell().Border(1).Padding(5).Text("");
					//table.Cell().Border(1).Padding(5).Text("");


					//table.Cell().Border(1).Padding(5).Text("4");
					//table.Cell().Border(1).Padding(5).Text("1237");
					//table.Cell().Border(1).Padding(5).Text("Jignesh Jinjala");
					//table.Cell().Border(1).Padding(5).Text("01-01-2000");
					//table.Cell().Border(1).Padding(5).Text("8185654228");
					//table.Cell().Border(1).Padding(5).Text("Male");
					//table.Cell().Border(1).Padding(5).Text("Ahir");
					//table.Cell().Border(1).Padding(5).Text("OBC");
					//table.Cell().Border(1).Padding(5).Text("");
					//table.Cell().Border(1).Padding(5).Text("");
					//table.Cell().Border(1).Padding(5).Text("");
					//table.Cell().Border(1).Padding(5).Text("");


					//table.Cell().Border(1).Padding(5).Text("5");
					//table.Cell().Border(1).Padding(5).Text("1238");
					//table.Cell().Border(1).Padding(5).Text("Parth Ghhusar");
					//table.Cell().Border(1).Padding(5).Text("01-01-2000");
					//table.Cell().Border(1).Padding(5).Text("8185654228");
					//table.Cell().Border(1).Padding(5).Text("Male");
					//table.Cell().Border(1).Padding(5).Text("Ahir");
					//table.Cell().Border(1).Padding(5).Text("OBC");
					//table.Cell().Border(1).Padding(5).Text("");
					//table.Cell().Border(1).Padding(5).Text("");
					//table.Cell().Border(1).Padding(5).Text("");
					//table.Cell().Border(1).Padding(5).Text("");

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
