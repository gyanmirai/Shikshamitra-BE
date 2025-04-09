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
	public class StudentStrengthDivisionWiseSummary : IDocument
	{
		private readonly IReadOnlyList<Student> _student;
		public StudentStrengthDivisionWiseSummary(IReadOnlyList<Student> student)
		{
			_student = student;
		}
		public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

		public List<StudentDivisionCountDto> GetStudentCountByDvision()
		{
			var result = _student
					.GroupBy(s => new
					{
						StandardId = s.Standard.Id,
						StandardName = s.Standard.Name,
						DivisionId = s.Division.Id,
						DivisionName = s.Division.Name
					})
					.Select(g => new StudentDivisionCountDto
					{
						StandardName = g.Key.StandardName,
						Division = g.Key.DivisionName,
						Total = g.Count()
					})
					.OrderBy(x => x.StandardName)
					.ThenBy(x => x.Division)
					.ToList();

			// Add summary rows per standard
			List<StudentDivisionCountDto> finalResult = new();

			var groupedByStandard = result
	.GroupBy(x => x.StandardName)
	.OrderBy(g => g.Key);

			foreach (var group in groupedByStandard)
			{
				finalResult.AddRange(group);

				finalResult.Add(new StudentDivisionCountDto
				{
					StandardName = group.Key,
					Division = "Total",
					Total = group.Sum(x => x.Total)
				});
			}

			return finalResult;

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
				column.Item().PaddingTop(10).BorderBottom(1).BorderColor(Colors.Black);
				column.Item().PaddingTop(10).AlignCenter().Text("Student Strength Division-Wise Summary")
					.FontSize(16)
					.Bold();
				column.Item().PaddingTop(10).BorderBottom(1).BorderColor(Colors.Black);

				column.Item().PaddingTop(10).Text("Batch: 2024-2025; As on date: 17/08/2024")
					.FontSize(12)
					.ExtraBold();

				column.Item().PaddingTop(10).Table(table =>
				{
					table.ColumnsDefinition(columns =>
					{
						columns.RelativeColumn();
						columns.RelativeColumn();
						columns.RelativeColumn();
					});

					// Table Header
					table.Header(header =>
					{
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Class").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Division").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Total").Bold();
					});

					// Sample Data Rows (Replace with actual data dynamically)
					foreach (var item in GetStudentCountByDvision())
					{
						if (item.Division == "Total")
						{

							AddTotalRow(table, "Total",item.Total.ToString());
						}
						else
						{
							AddTableRow(table, item.StandardName, item.Division, item.Total.ToString());

						}

					}

					StudentDivisionCountDto GrandTotal = new StudentDivisionCountDto
					{
						StandardName = "Grand Total",
						Division = " ",
						Total = GetStudentCountByDvision().Where(x => x.Division != "Total" && x.StandardName != "Grand Total").Sum(x => x.Total)
					};
					// Add similar rows for other grades...

					AddTotalRow(table, GrandTotal.StandardName, GrandTotal.Total.ToString()); // Grand Total
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

		private void AddTableRow(TableDescriptor table, string className, string division, string total)
		{
			table.Cell().Border(1).Padding(5).Text(className);
			table.Cell().Border(1).Padding(5).Text(division);
			table.Cell().Border(1).Padding(5).Text(total);
		}

		private void AddTotalRow(TableDescriptor table, string columnName, string total)
		{
			table.Cell().ColumnSpan(2).Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text(columnName).Bold();
			table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text(total).Bold();
		}
	}
}
public class StudentDivisionCountDto
{
	public string StandardName { get; set; }
	public string Division { get; set; }
	public int Total { get; set; }
}