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
	public class ReligionwiseList : IDocument
	{

		private readonly IReadOnlyList<Student> _students;
		private readonly Dictionary<string, List<Student>> _groupByreligonData;

		public ReligionwiseList(IReadOnlyList<Student> students)
		{
			_students = students;
			_groupByreligonData = ConvertReligionData();
		}
		public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

		public Dictionary<string, List<Student>> ConvertReligionData()
		{
			Dictionary<string, List<Student>> groupedByReligion = _students
			.GroupBy(s => s.Religion)
			.ToDictionary(g => g.Key, g => g.ToList());
			return groupedByReligion;
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
						c.Item().Text("Header PArt");
					});
				});
			});
		}

		void ComposeContent(IContainer container)
		{
			container.PaddingVertical(20).Column(column =>
			{
				column.Item().PaddingTop(10).BorderBottom(1).BorderColor(Colors.Black);

				column.Item().PaddingTop(10).AlignCenter().Text("Religionwise List")
					.FontSize(16).Bold();

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
					AddDetailRow(table, "As on Date", "17/08/2024");



				});

				foreach (var kvp in _groupByreligonData)
				{
					
					column.Item().PaddingTop(20).Text(kvp.Key)
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
							header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Sr. No.").Bold();
							header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("GR No.").Bold();
							header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Roll. No").Bold();
							header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Name").Bold();
						});

						// Sample data rows
						foreach (var (student,index) in kvp.Value.Select((val, index) => (val, index + 1)))
						{
							
							table.Cell().Border(1).Padding(5).Text(index);
							table.Cell().Border(1).Padding(5).Text(student.GrNoRecord);
							table.Cell().Border(1).Padding(5).Text(student.RollNo);
							table.Cell().Border(1).Padding(5).Text(student.LastName + " " + student.FirstName + " " + student.MiddleName);
						}
						

						table.Cell().Background(Colors.Grey.Lighten2).Border(1).Padding(5).Text("Total").Bold();
						table.Cell().Background(Colors.Grey.Lighten2).Border(1).Padding(5).Text("").Bold();
						table.Cell().Background(Colors.Grey.Lighten2).Border(1).Padding(5).Text("").Bold();
						table.Cell().Background(Colors.Grey.Lighten2).Border(1).Padding(5).Text(kvp.Value.Count()).Bold();

					});
					
				}

			
				
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
